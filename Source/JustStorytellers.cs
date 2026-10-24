using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace JustStorytellers;

public class JustStorytellersSettings : ModSettings
{
    // storyteller defName -> source, missing means the default (our art for the vanilla three, its own for the rest)
    // sources: "self", "teller:<defName>", "js:<file>", "own:<file name>"
    public Dictionary<string, string> sources = new Dictionary<string, string>();

    // own file name -> where its small portrait is cut from, "centerX centerY width" as parts of the image
    public Dictionary<string, string> crops = new Dictionary<string, string>();

    // hides the six pictures of this mod from the choices
    public bool hideOurArt;

    public string Get(string defName) => sources.TryGetValue(defName, out string s) ? s : Sources.DefaultSource(defName);

    public override void ExposeData()
    {
        if (Scribe.mode == LoadSaveMode.LoadingVars)
        {
            // settings from before any storyteller could be changed
            Dictionary<string, string> old = null;
            Scribe_Collections.Look(ref old, "choices", LookMode.Value, LookMode.Value);
            if (old != null)
            {
                foreach (KeyValuePair<string, string> pair in old)
                {
                    string file = Sources.OurFile(pair.Key);
                    if (file == null)
                    {
                        continue;
                    }
                    sources[pair.Key] = pair.Value switch
                    {
                        "Vanilla" => Sources.Self,
                        "Alternative" => Sources.JsPrefix + file + "Alt",
                        _ => Sources.JsPrefix + file
                    };
                }
            }
        }
        Dictionary<string, string> loaded = sources;
        Scribe_Collections.Look(ref loaded, "sources", LookMode.Value, LookMode.Value);
        if (Scribe.mode == LoadSaveMode.LoadingVars && loaded != null)
        {
            foreach (KeyValuePair<string, string> pair in loaded)
            {
                sources[pair.Key] = pair.Value;
            }
        }
        Scribe_Collections.Look(ref crops, "crops", LookMode.Value, LookMode.Value);
        crops ??= new Dictionary<string, string>();
        Scribe_Values.Look(ref hideOurArt, "hideOurArt", false);
    }
}

// kept apart from Portraits so loading the settings doesn't run its startup code too early
public static class Sources
{
    public const string Self = "self";
    public const string TellerPrefix = "teller:";
    public const string JsPrefix = "js:";
    public const string OwnPrefix = "own:";

    // vanilla storytellers that have our art, defName -> file
    public static readonly Dictionary<string, string> OurFiles = new Dictionary<string, string>
    {
        { "Cassandra", "CassandraClassic" },
        { "Phoebe", "PhoebeFriendly" },
        { "Randy", "RandyRandom" }
    };

    public static string OurFile(string defName) => OurFiles.TryGetValue(defName, out string file) ? file : null;

    public static string DefaultSource(string defName)
    {
        string file = OurFile(defName);
        return file == null ? Self : JsPrefix + file;
    }
}

public class JustStorytellersMod : Mod
{
    public static JustStorytellersSettings settings;

    StorytellerDef selected;
    Vector2 listScroll;
    Vector2 choiceScroll;
    int lastDrawFrame = -10;
    string dragFile;
    Vector2 dragOffset;
    Vector3 dragCrop;

    public JustStorytellersMod(ModContentPack content) : base(content)
    {
        settings = GetSettings<JustStorytellersSettings>();
    }

    public override string SettingsCategory() => "JustStorytellers";

    public override void DoSettingsWindowContents(Rect inRect)
    {
        // the page was just opened, look for new images
        if (Time.frameCount - lastDrawFrame > 2)
        {
            Portraits.RefreshOwnImages();
        }
        lastDrawFrame = Time.frameCount;

        List<StorytellerDef> tellers = Portraits.Tellers;
        if (tellers.Count == 0)
        {
            return;
        }
        if (selected == null || !tellers.Contains(selected))
        {
            selected = tellers[0];
        }

        Rect left = new Rect(inRect.x, inRect.y, 210f, inRect.height - 34f);
        Rect right = new Rect(left.xMax + 16f, inRect.y, inRect.width - left.width - 16f, inRect.height);
        DrawList(left, tellers);
        Widgets.CheckboxLabeled(new Rect(left.x, left.yMax + 8f, left.width, 26f), "Hide this mod alt images", ref settings.hideOurArt);
        DrawChoices(right);
    }

    void DrawList(Rect rect, List<StorytellerDef> tellers)
    {
        const float rowHeight = 44f;
        Widgets.DrawMenuSection(rect);
        Rect outRect = rect.ContractedBy(4f);
        Rect viewRect = new Rect(0f, 0f, outRect.width - 16f, tellers.Count * rowHeight);
        Widgets.BeginScrollView(outRect, ref listScroll, viewRect);
        float y = 0f;
        foreach (StorytellerDef def in tellers)
        {
            Rect row = new Rect(0f, y, viewRect.width, rowHeight);
            if (def == selected)
            {
                Widgets.DrawHighlightSelected(row);
            }
            Widgets.DrawHighlightIfMouseover(row);
            Texture2D tiny = def.portraitTinyTex ?? def.portraitLargeTex;
            if (tiny != null)
            {
                GUI.DrawTexture(new Rect(row.x + 4f, row.y + 3f, 36f, 38f), tiny, ScaleMode.ScaleToFit);
            }
            Text.Anchor = TextAnchor.MiddleLeft;
            Widgets.Label(new Rect(row.x + 48f, row.y, row.width - 48f, row.height), def.LabelCap);
            Text.Anchor = TextAnchor.UpperLeft;
            if (Widgets.ButtonInvisible(row))
            {
                selected = def;
                choiceScroll = Vector2.zero;
            }
            y += rowHeight;
        }
        Widgets.EndScrollView();
    }

    void DrawChoices(Rect rect)
    {
        string current = settings.Get(selected.defName);

        // own images get a bigger picture to pick the small portrait from
        string ownFile = current.StartsWith(Sources.OwnPrefix) ? current.Substring(Sources.OwnPrefix.Length) : null;
        Texture2D ownTex = null;
        bool cropping = ownFile != null && Portraits.OwnImages.TryGetValue(ownFile, out ownTex);

        // current portrait
        Rect portrait = cropping ? new Rect(rect.x, rect.y, 220f, 235f) : new Rect(rect.x, rect.y, 145f, 155f);
        Widgets.DrawBoxSolid(portrait, new Color(1f, 1f, 1f, 0.05f));
        if (cropping)
        {
            // stretched like the game draws it
            GUI.DrawTexture(portrait, ownTex);
        }
        else if (selected.portraitLargeTex != null)
        {
            GUI.DrawTexture(portrait, selected.portraitLargeTex, ScaleMode.ScaleToFit);
        }
        Widgets.DrawBox(portrait);

        float x = portrait.xMax + 16f;
        float textWidth = rect.xMax - x - (cropping ? 140f : 0f);
        Text.Font = GameFont.Medium;
        Widgets.Label(new Rect(x, rect.y, textWidth, 34f), selected.LabelCap);
        Text.Font = GameFont.Small;
        Widgets.Label(new Rect(x, rect.y + 38f, textWidth, 24f), "Using: " + Portraits.Label(current));
        if (Widgets.ButtonText(new Rect(x, rect.y + 70f, 160f, 32f), "Back to its own"))
        {
            Choose(Sources.Self);
        }
        if (Widgets.ButtonText(new Rect(x, rect.y + 110f, 160f, 32f), "Open folder"))
        {
            Portraits.OpenOwnFolder();
        }
        if (cropping)
        {
            DrawCrop(portrait, new Rect(rect.xMax - 126f, rect.y + 30f, 122f, 130f), ownFile, ownTex);
            GUI.color = Color.gray;
            Text.Font = GameFont.Tiny;
            Widgets.Label(new Rect(x, rect.y + 152f, textWidth, 60f), "Click or drag on the picture to pick the small portrait. Scroll on it to change the size.");
            Text.Font = GameFont.Small;
            GUI.color = Color.white;
        }

        // choices
        const float tileWidth = 70f;
        const float tileHeight = 75f;
        const float gap = 8f;
        const float headerHeight = 30f;
        Rect outRect = new Rect(rect.x, portrait.yMax + 12f, rect.width, rect.yMax - portrait.yMax - 12f);
        float viewWidth = outRect.width - 16f;
        int perRow = Mathf.Max(1, (int)((viewWidth + gap) / (tileWidth + gap)));

        List<(string title, List<string> items, string empty)> groups = new List<(string, List<string>, string)>
        {
            (selected.LabelCap, Portraits.OwnOptions(selected).ToList(), null),
            ("Other storytellers", Portraits.Tellers.Where(d => d != selected).SelectMany(Portraits.OtherOptions).ToList(), null),
            ("Your images", Portraits.OwnImages.Keys.Select(f => Portraits.OwnPrefix + f).ToList(), "Put PNG or JPG files in the folder and they show up here. 580x620 fits best.")
        };

        float height = 0f;
        foreach (var group in groups)
        {
            int rows = (group.items.Count + perRow - 1) / perRow;
            height += headerHeight + (group.items.Count == 0 ? 30f : rows * (tileHeight + gap)) + 8f;
        }

        Rect viewRect = new Rect(0f, 0f, viewWidth, height);
        Widgets.BeginScrollView(outRect, ref choiceScroll, viewRect);
        float y = 0f;
        foreach (var group in groups)
        {
            Widgets.Label(new Rect(0f, y + 4f, viewWidth, 24f), group.title);
            Widgets.DrawLineHorizontal(0f, y + headerHeight - 2f, viewWidth);
            y += headerHeight;
            if (group.items.Count == 0)
            {
                GUI.color = Color.gray;
                Widgets.Label(new Rect(0f, y, viewWidth, 30f), group.empty ?? "");
                GUI.color = Color.white;
                y += 30f;
            }
            for (int i = 0; i < group.items.Count; i++)
            {
                string source = group.items[i];
                Rect tile = new Rect(i % perRow * (tileWidth + gap), y + i / perRow * (tileHeight + gap), tileWidth, tileHeight);
                Widgets.DrawBoxSolid(tile, new Color(1f, 1f, 1f, 0.05f));
                Portraits.Resolve(selected, source, out _, out Texture2D tiny);
                if (tiny != null)
                {
                    GUI.DrawTexture(tile, tiny, ScaleMode.ScaleToFit);
                }
                Widgets.DrawBox(tile, source == current ? 3 : 1);
                Widgets.DrawHighlightIfMouseover(tile);
                TooltipHandler.TipRegion(tile, Portraits.Label(source));
                if (Widgets.ButtonInvisible(tile))
                {
                    Choose(source);
                }
            }
            y += (group.items.Count + perRow - 1) / perRow * (tileHeight + gap) + 8f;
        }
        Widgets.EndScrollView();
    }

    void DrawCrop(Rect box, Rect preview, string file, Texture2D tex)
    {
        Vector3 crop = dragFile == file ? dragCrop : Portraits.GetCrop(file, tex);
        Rect r = Portraits.CropRect(tex, crop);
        Rect square = new Rect(box.x + r.x * box.width, box.y + r.y * box.height, r.width * box.width, r.height * box.height);

        Event e = Event.current;
        Vector2 mouse = e.mousePosition;
        if (e.type == EventType.MouseDown && e.button == 0 && box.Contains(mouse))
        {
            // grabbing the square keeps it under the mouse, clicking elsewhere centers it there
            dragFile = file;
            dragOffset = square.Contains(mouse) ? square.center - mouse : Vector2.zero;
            e.Use();
        }
        if (dragFile == file)
        {
            Vector2 center = mouse + dragOffset;
            crop = Portraits.ClampCrop(tex, new Vector3((center.x - box.x) / box.width, (center.y - box.y) / box.height, crop.z));
            dragCrop = crop;
            if (e.rawType == EventType.MouseUp || !Input.GetMouseButton(0))
            {
                dragFile = null;
                Portraits.SetCrop(file, crop);
            }
        }
        else if (e.type == EventType.ScrollWheel && box.Contains(mouse))
        {
            crop.z *= 1f + e.delta.y * 0.03f;
            Portraits.SetCrop(file, crop);
            crop = Portraits.GetCrop(file, tex);
            e.Use();
        }

        r = Portraits.CropRect(tex, crop);
        square = new Rect(box.x + r.x * box.width, box.y + r.y * box.height, r.width * box.width, r.height * box.height);
        GUI.color = Color.black;
        Widgets.DrawBox(square.ExpandedBy(1f), 1);
        GUI.color = Color.white;
        Widgets.DrawBox(square, 2);

        Text.Anchor = TextAnchor.UpperCenter;
        Widgets.Label(new Rect(preview.x - 20f, preview.y - 26f, preview.width + 40f, 24f), "Small portrait");
        Text.Anchor = TextAnchor.UpperLeft;
        Widgets.DrawBoxSolid(preview, new Color(1f, 1f, 1f, 0.05f));
        GUI.DrawTextureWithTexCoords(preview, tex, new Rect(r.x, 1f - r.y - r.height, r.width, r.height));
        Widgets.DrawBox(preview);
    }

    void Choose(string source)
    {
        if (source == Sources.DefaultSource(selected.defName))
        {
            settings.sources.Remove(selected.defName);
        }
        else
        {
            settings.sources[selected.defName] = source;
        }
        Portraits.Apply(selected);
    }
}

[StaticConstructorOnStartup]
public static class Portraits
{
    public const string Self = Sources.Self;
    public const string TellerPrefix = Sources.TellerPrefix;
    public const string JsPrefix = Sources.JsPrefix;
    public const string OwnPrefix = Sources.OwnPrefix;


    public static readonly List<StorytellerDef> Tellers = new List<StorytellerDef>();
    static readonly Dictionary<StorytellerDef, (Texture2D large, Texture2D tiny)> originals = new Dictionary<StorytellerDef, (Texture2D, Texture2D)>();

    // own file name -> texture, sorted by name
    public static readonly SortedDictionary<string, Texture2D> OwnImages = new SortedDictionary<string, Texture2D>(StringComparer.OrdinalIgnoreCase);
    static readonly Dictionary<string, DateTime> ownTimes = new Dictionary<string, DateTime>();
    static readonly Dictionary<string, Texture2D> ownTiny = new Dictionary<string, Texture2D>(StringComparer.OrdinalIgnoreCase);

    // around the face on a portrait like the vanilla ones
    static readonly Vector3 defaultCrop = new Vector3(0.5f, 0.3f, 0.42f);

    public static string OwnFolder => Path.Combine(GenFilePaths.SaveDataFolderPath, "JustStorytellers");

    // runs after the storytellers loaded their vanilla portraits
    static Portraits()
    {
        foreach (StorytellerDef def in DefDatabase<StorytellerDef>.AllDefs.OrderBy(d => d.listOrder).ThenBy(d => d.label))
        {
            originals[def] = (def.portraitLargeTex, def.portraitTinyTex);
            // hidden ones like the tutorial storyteller can't be picked anyway
            if (def.listVisible)
            {
                Tellers.Add(def);
            }
        }
        RefreshOwnImages();
        foreach (StorytellerDef def in Tellers)
        {
            Apply(def);
        }
    }

    // what a storyteller can use from itself: its own portrait and our art for it
    public static IEnumerable<string> OwnOptions(StorytellerDef def)
    {
        yield return Self;
        foreach (string js in JsOptions(def))
        {
            yield return js;
        }
    }

    // what other storytellers can borrow from this one
    public static IEnumerable<string> OtherOptions(StorytellerDef def)
    {
        if (originals.TryGetValue(def, out var o) && o.large != null)
        {
            yield return TellerPrefix + def.defName;
        }
        foreach (string js in JsOptions(def))
        {
            yield return js;
        }
    }

    static IEnumerable<string> JsOptions(StorytellerDef def)
    {
        string file = Sources.OurFile(def.defName);
        if (file != null && !JustStorytellersMod.settings.hideOurArt)
        {
            yield return JsPrefix + file;
            yield return JsPrefix + file + "Alt";
        }
    }

    public static void Apply(StorytellerDef def)
    {
        if (!Resolve(def, JustStorytellersMod.settings.Get(def.defName), out Texture2D large, out Texture2D tiny))
        {
            // the storyteller or file is gone, use its own
            Resolve(def, Self, out large, out tiny);
        }
        def.portraitLargeTex = large;
        def.portraitTinyTex = tiny;
    }

    public static bool Resolve(StorytellerDef def, string source, out Texture2D large, out Texture2D tiny)
    {
        large = null;
        tiny = null;
        if (source.StartsWith(TellerPrefix))
        {
            StorytellerDef other = DefDatabase<StorytellerDef>.GetNamedSilentFail(source.Substring(TellerPrefix.Length));
            if (other == null || !originals.TryGetValue(other, out var o) || o.large == null)
            {
                return false;
            }
            large = o.large;
            tiny = o.tiny ?? o.large;
            return true;
        }
        if (source.StartsWith(JsPrefix))
        {
            string file = source.Substring(JsPrefix.Length);
            large = ContentFinder<Texture2D>.Get("JustStorytellers/" + file, false);
            tiny = ContentFinder<Texture2D>.Get("JustStorytellers/" + file + "Tiny", false) ?? large;
            return large != null;
        }
        if (source.StartsWith(OwnPrefix))
        {
            if (!OwnImages.TryGetValue(source.Substring(OwnPrefix.Length), out Texture2D tex))
            {
                return false;
            }
            large = tex;
            tiny = ownTiny.TryGetValue(source.Substring(OwnPrefix.Length), out Texture2D small) ? small : tex;
            return true;
        }
        if (originals.TryGetValue(def, out var mine))
        {
            large = mine.large;
            tiny = mine.tiny;
        }
        return true;
    }

    public static string Label(string source)
    {
        if (source.StartsWith(TellerPrefix))
        {
            StorytellerDef other = DefDatabase<StorytellerDef>.GetNamedSilentFail(source.Substring(TellerPrefix.Length));
            return other != null ? other.LabelCap.ToString() : source.Substring(TellerPrefix.Length);
        }
        if (source.StartsWith(JsPrefix))
        {
            string file = source.Substring(JsPrefix.Length);
            string name = Sources.OurFiles.FirstOrDefault(p => file.StartsWith(p.Value)).Key ?? file;
            return file.EndsWith("Alt") ? name + " alternative" : name + " JustStorytellers";
        }
        if (source.StartsWith(OwnPrefix))
        {
            return source.Substring(OwnPrefix.Length);
        }
        return "Its own";
    }

    public static void RefreshOwnImages()
    {
        string folder = OwnFolder;
        HashSet<string> found = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            if (Directory.Exists(folder))
            {
                foreach (string path in Directory.GetFiles(folder))
                {
                    string ext = Path.GetExtension(path).ToLowerInvariant();
                    if (ext != ".png" && ext != ".jpg" && ext != ".jpeg")
                    {
                        continue;
                    }
                    string name = Path.GetFileName(path);
                    found.Add(name);
                    DateTime time = File.GetLastWriteTimeUtc(path);
                    // already loaded, or already failed to load
                    if (ownTimes.TryGetValue(name, out DateTime old) && old == time)
                    {
                        continue;
                    }
                    ownTimes[name] = time;
                    Texture2D tex = new Texture2D(2, 2, TextureFormat.RGBA32, true);
                    if (!tex.LoadImage(File.ReadAllBytes(path)))
                    {
                        Log.Warning("[JustStorytellers] Could not load " + path);
                        OwnImages.Remove(name);
                        continue;
                    }
                    tex.name = Path.GetFileNameWithoutExtension(name);
                    tex.wrapMode = TextureWrapMode.Clamp;
                    OwnImages[name] = tex;
                    BuildTiny(name, tex, GetCrop(name, tex));
                }
            }
        }
        catch (Exception e)
        {
            Log.Warning("[JustStorytellers] Could not read " + folder + ": " + e.Message);
        }

        bool changed = false;
        foreach (string name in ownTimes.Keys.Where(n => !found.Contains(n)).ToList())
        {
            changed |= OwnImages.Remove(name);
            ownTiny.Remove(name);
            ownTimes.Remove(name);
        }
        // an image in use may have changed or gone away
        if (JustStorytellersMod.settings != null && Tellers.Count > 0)
        {
            foreach (StorytellerDef def in Tellers)
            {
                if (changed || JustStorytellersMod.settings.Get(def.defName).StartsWith(OwnPrefix))
                {
                    Apply(def);
                }
            }
        }
    }

    // height of the small portrait as a part of the image, for each part of its width
    static float CropAspect(Texture2D tex) => Storyteller.PortraitSizeTiny.y / Storyteller.PortraitSizeTiny.x * tex.width / tex.height;

    public static Rect CropRect(Texture2D tex, Vector3 crop)
    {
        float h = crop.z * CropAspect(tex);
        return new Rect(crop.x - crop.z / 2f, crop.y - h / 2f, crop.z, h);
    }

    public static Vector3 ClampCrop(Texture2D tex, Vector3 crop)
    {
        float aspect = CropAspect(tex);
        float w = Mathf.Clamp(crop.z, 0.05f, Mathf.Min(1f, 1f / aspect));
        float h = w * aspect;
        return new Vector3(Mathf.Clamp(crop.x, w / 2f, 1f - w / 2f), Mathf.Clamp(crop.y, h / 2f, 1f - h / 2f), w);
    }

    public static Vector3 GetCrop(string file, Texture2D tex)
    {
        Vector3 crop = defaultCrop;
        if (JustStorytellersMod.settings.crops.TryGetValue(file, out string saved))
        {
            string[] parts = saved.Split(' ');
            if (parts.Length == 3
                && float.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out float cx)
                && float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float cy)
                && float.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out float w))
            {
                crop = new Vector3(cx, cy, w);
            }
        }
        return ClampCrop(tex, crop);
    }

    public static void SetCrop(string file, Vector3 crop)
    {
        if (!OwnImages.TryGetValue(file, out Texture2D tex))
        {
            return;
        }
        crop = ClampCrop(tex, crop);
        JustStorytellersMod.settings.crops[file] = string.Format(CultureInfo.InvariantCulture, "{0} {1} {2}", crop.x, crop.y, crop.z);
        BuildTiny(file, tex, crop);
    }

    // cuts the small portrait out of the image, the same texture is reused so it updates everywhere
    static void BuildTiny(string file, Texture2D tex, Vector3 crop)
    {
        int w = (int)Storyteller.PortraitSizeTiny.x;
        int h = (int)Storyteller.PortraitSizeTiny.y;
        try
        {
            Rect r = CropRect(tex, crop);
            int sw = Mathf.Clamp(Mathf.RoundToInt(r.width * tex.width), 1, tex.width);
            int sh = Mathf.Clamp(Mathf.RoundToInt(r.height * tex.height), 1, tex.height);
            int sx = Mathf.Clamp(Mathf.RoundToInt(r.x * tex.width), 0, tex.width - sw);
            // pixels go bottom up
            int sy = Mathf.Clamp(tex.height - Mathf.RoundToInt(r.y * tex.height) - sh, 0, tex.height - sh);
            Color[] src = tex.GetPixels(sx, sy, sw, sh);
            Color[] dst = new Color[w * h];
            for (int y = 0; y < h; y++)
            {
                int y0 = y * sh / h;
                int y1 = Math.Max(y0 + 1, (y + 1) * sh / h);
                for (int x = 0; x < w; x++)
                {
                    int x0 = x * sw / w;
                    int x1 = Math.Max(x0 + 1, (x + 1) * sw / w);
                    // averaged with alpha weight so transparent edges don't bleed
                    float red = 0f, green = 0f, blue = 0f, alpha = 0f;
                    for (int yy = y0; yy < y1; yy++)
                    {
                        for (int xx = x0; xx < x1; xx++)
                        {
                            Color c = src[yy * sw + xx];
                            red += c.r * c.a;
                            green += c.g * c.a;
                            blue += c.b * c.a;
                            alpha += c.a;
                        }
                    }
                    int count = (y1 - y0) * (x1 - x0);
                    dst[y * w + x] = alpha > 0f ? new Color(red / alpha, green / alpha, blue / alpha, alpha / count) : Color.clear;
                }
            }
            if (!ownTiny.TryGetValue(file, out Texture2D tiny))
            {
                tiny = new Texture2D(w, h, TextureFormat.RGBA32, false);
                tiny.name = tex.name + "Tiny";
                tiny.wrapMode = TextureWrapMode.Clamp;
                ownTiny[file] = tiny;
            }
            tiny.SetPixels(dst);
            tiny.Apply(false);
        }
        catch (Exception e)
        {
            Log.Warning("[JustStorytellers] Could not make the small portrait of " + file + ": " + e.Message);
            ownTiny.Remove(file);
        }
    }

    public static void OpenOwnFolder()
    {
        string folder = OwnFolder;
        try
        {
            Directory.CreateDirectory(folder);
            string program = OperatingSystem.IsWindows() ? "explorer.exe" : OperatingSystem.IsMacOS() ? "open" : "xdg-open";
            Process.Start(new ProcessStartInfo(program) { ArgumentList = { folder }, UseShellExecute = false });
        }
        catch (Exception e)
        {
            Log.Warning("[JustStorytellers] Could not open " + folder + ": " + e.Message);
        }
    }
}
