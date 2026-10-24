using System;
using System.Collections.Generic;
using System.Diagnostics;
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

        Rect left = new Rect(inRect.x, inRect.y, 210f, inRect.height);
        Rect right = new Rect(left.xMax + 16f, inRect.y, inRect.width - left.width - 16f, inRect.height);
        DrawList(left, tellers);
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

        // current portrait
        Rect portrait = new Rect(rect.x, rect.y, 145f, 155f);
        Widgets.DrawBoxSolid(portrait, new Color(1f, 1f, 1f, 0.05f));
        if (selected.portraitLargeTex != null)
        {
            GUI.DrawTexture(portrait, selected.portraitLargeTex, ScaleMode.ScaleToFit);
        }
        Widgets.DrawBox(portrait);

        float x = portrait.xMax + 16f;
        Text.Font = GameFont.Medium;
        Widgets.Label(new Rect(x, rect.y, rect.xMax - x, 34f), selected.LabelCap);
        Text.Font = GameFont.Small;
        Widgets.Label(new Rect(x, rect.y + 38f, rect.xMax - x, 24f), "Using: " + Portraits.Label(current));
        if (Widgets.ButtonText(new Rect(x, rect.y + 70f, 160f, 32f), "Back to its own"))
        {
            Choose(Sources.Self);
        }
        if (Widgets.ButtonText(new Rect(x, rect.y + 110f, 160f, 32f), "Open folder"))
        {
            Portraits.OpenOwnFolder();
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
        if (file != null)
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
            tiny = tex;
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
