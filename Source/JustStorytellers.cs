using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace JustStorytellers;

public enum PortraitChoice
{
    JustStorytellers,
    Alternative,
    Vanilla
}

public class JustStorytellersSettings : ModSettings
{
    // storyteller defName -> choice, missing means JustStorytellers
    public Dictionary<string, PortraitChoice> choices = new Dictionary<string, PortraitChoice>();

    public PortraitChoice Get(string defName) => choices.TryGetValue(defName, out PortraitChoice c) ? c : PortraitChoice.JustStorytellers;

    public override void ExposeData()
    {
        Scribe_Collections.Look(ref choices, "choices", LookMode.Value, LookMode.Value);
        choices ??= new Dictionary<string, PortraitChoice>();
    }
}

public class JustStorytellersMod : Mod
{
    public static JustStorytellersSettings settings;

    public JustStorytellersMod(ModContentPack content) : base(content)
    {
        settings = GetSettings<JustStorytellersSettings>();
    }

    public override string SettingsCategory() => "JustStorytellers";

    public override void DoSettingsWindowContents(Rect inRect)
    {
        const float size = 116f;
        const float rowHeight = size + 60f;
        float y = inRect.y;
        foreach (Portraits.Entry entry in Portraits.Entries)
        {
            if (entry.def == null)
            {
                continue;
            }
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(inRect.x, y, 300f, 32f), entry.def.LabelCap);
            Text.Font = GameFont.Small;
            float x = inRect.x;
            foreach (PortraitChoice choice in new[] { PortraitChoice.JustStorytellers, PortraitChoice.Alternative, PortraitChoice.Vanilla })
            {
                Rect box = new Rect(x, y + 34f, size, size * 124f / 116f);
                bool selected = settings.Get(entry.def.defName) == choice;
                Widgets.DrawBoxSolid(box, new Color(1f, 1f, 1f, 0.05f));
                GUI.DrawTexture(box, entry.Tiny(choice), ScaleMode.ScaleToFit);
                Widgets.DrawBox(box, selected ? 3 : 1);
                Widgets.DrawHighlightIfMouseover(box);
                Text.Anchor = TextAnchor.UpperCenter;
                Widgets.Label(new Rect(box.x - 10f, box.yMax + 2f, box.width + 20f, 24f), Portraits.Label(choice));
                Text.Anchor = TextAnchor.UpperLeft;
                if (Widgets.ButtonInvisible(box))
                {
                    settings.choices[entry.def.defName] = choice;
                    Portraits.Apply(entry);
                }
                x += size + 30f;
            }
            y += rowHeight + 16f;
        }
    }
}

[StaticConstructorOnStartup]
public static class Portraits
{
    public class Entry
    {
        public StorytellerDef def;
        public string file;
        public Texture2D vanillaLarge;
        public Texture2D vanillaTiny;

        public Texture2D Large(PortraitChoice c) => c switch
        {
            PortraitChoice.Vanilla => vanillaLarge,
            PortraitChoice.Alternative => ContentFinder<Texture2D>.Get("JustStorytellers/" + file + "Alt"),
            _ => ContentFinder<Texture2D>.Get("JustStorytellers/" + file)
        };

        public Texture2D Tiny(PortraitChoice c) => c switch
        {
            PortraitChoice.Vanilla => vanillaTiny,
            PortraitChoice.Alternative => ContentFinder<Texture2D>.Get("JustStorytellers/" + file + "AltTiny"),
            _ => ContentFinder<Texture2D>.Get("JustStorytellers/" + file + "Tiny")
        };
    }

    public static readonly List<Entry> Entries = new List<Entry>();

    // runs after the storytellers loaded their vanilla portraits
    static Portraits()
    {
        Add("Cassandra", "CassandraClassic");
        Add("Phoebe", "PhoebeFriendly");
        Add("Randy", "RandyRandom");
        foreach (Entry entry in Entries)
        {
            Apply(entry);
        }
    }

    static void Add(string defName, string file)
    {
        StorytellerDef def = DefDatabase<StorytellerDef>.GetNamedSilentFail(defName);
        if (def == null)
        {
            return;
        }
        Entries.Add(new Entry { def = def, file = file, vanillaLarge = def.portraitLargeTex, vanillaTiny = def.portraitTinyTex });
    }

    public static void Apply(Entry entry)
    {
        PortraitChoice choice = JustStorytellersMod.settings.Get(entry.def.defName);
        entry.def.portraitLargeTex = entry.Large(choice);
        entry.def.portraitTinyTex = entry.Tiny(choice);
    }

    public static string Label(PortraitChoice c) => c switch
    {
        PortraitChoice.Alternative => "Alternative",
        PortraitChoice.Vanilla => "Vanilla",
        _ => "JustStorytellers"
    };
}
