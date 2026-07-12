using Menu.Remix.MixedUI;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace TrueParallax;

public partial class Options
{
    #region UI
    private const string DEFAULT_PRESET = "Default";
    private const string DEFAULT_DESCRIPTION = "The default, recommended settings for this mod.";
    private OpListBox presetsBox;
    private OpTextBox saveNameBox;
    private OpHoldButton loadButton, saveButton, fileButton;
    public void SetupPresetsTab()
    {
        OpTab tab = this.Tabs.FirstOrDefault(t => t.name == PRESETS);
        if (tab == null)
        {
            Plugin.Error("Could not find Presets tab!!");
            return;
        }

        tab.AddItems(
            new OpLabel(50, 500, "Load Preset", true),
            presetsBox = new(new Configurable<string>(""), new(50, 460), 200, GetAllPresets(), 5, true),
            loadButton = new(new(350, 450), new UnityEngine.Vector2(150, 50), "Load Preset", 40) { description = "Overrides all your current configs with the selected preset."},

            new OpLabel(50, 300, "Save Preset", true),
            saveNameBox = new(new Configurable<string>("My Preset"), new(50, 250), 200) { description = "The name of the preset you would like to save. Using the name of an existing preset will overwrite it."},
            saveButton = new(new(350, 250), new UnityEngine.Vector2(150, 50), "Save Preset", 40) { description = "Saves your current options as a preset, overwriting any former presets with that name."},

            fileButton = new(new(200, 100), new UnityEngine.Vector2(150, 50), "View Presets Folder") { description = "View your presets in the file explorer."}
            );

        presetsBox.PosY -= presetsBox._rectList.size.y; //move down so that the list extends BELOW the original y coordinate

        presetsBox.OnValueChanged += PresetsBox_OnValueChanged;
        loadButton.OnPressDone += LoadButton_OnPressDone;
        saveButton.OnPressDone += SaveButton_OnPressDone;
        fileButton.OnPressDone += FileButton_OnPressDone;
    }

    public void PresetsUpdate()
    {
        loadButton.greyedOut = presetsBox.value == ""; //grey out when no preset is selected
        saveButton.greyedOut = saveNameBox.value == "";
    }


    private void PresetsBox_OnValueChanged(UIconfig config, string value, string oldValue)
    {
        //saveNameBox.value = value;
        string v = presetsBox._GetDisplayValue();
        saveNameBox.value = (v == DEFAULT_PRESET || v == "") ? "My Preset" : v;
    }

    private void LoadButton_OnPressDone(UIfocusable trigger)
    {
        trigger.Menu.PlaySound(SoundID.MENU_Button_Standard_Button_Pressed);
        LoadPreset(presetsBox.value);
    }

    private void SaveButton_OnPressDone(UIfocusable trigger)
    {
        trigger.Menu.PlaySound(SoundID.MENU_Button_Standard_Button_Pressed);

        string name = saveNameBox.value;
        string desc = "";
        ListItem existingPreset = presetsBox._itemList.FirstOrDefault(i => i.displayName == name);
        if (existingPreset != default)
        {
            desc = existingPreset.desc;
            Plugin.Log($"Found decription for preset {name}: {desc}", 2);
        }
        SavePreset(name, desc);

        try
        {
            //replace the preset list
            ListItem[] oldItems = presetsBox._itemList;
            presetsBox.AddItems(true, GetAllPresets().ToArray());
            presetsBox.RemoveItems(false, oldItems.Select(i => i.name).ToArray());
        }
        catch (Exception ex) { Plugin.Error(ex); }
    }

    private void FileButton_OnPressDone(UIfocusable trigger)
    {
        trigger.Menu.PlaySound(SoundID.MENU_Button_Standard_Button_Pressed);

        try
        {
            if (!Directory.Exists(PresetDirectory))
                Directory.CreateDirectory(PresetDirectory); //make sure there's actually a folder to open

            System.Diagnostics.Process.Start(PresetDirectory);
        }
        catch (Exception ex) { Plugin.Error(ex); }
    }

    #endregion

    #region Files
    public const string PRESET_SUBFOLDER = "ParallaxPresets";
    private string PresetDirectory = Path.Combine(OptionInterface.ConfigHolder.configDirPath, PRESET_SUBFOLDER);

    public const char PRESET_SEPARATOR = '=';
    private const string PRESET_DESCRIPTION_KEY = "PRESET_DESCRIPTION";

    public List<ListItem> GetAllPresets()
    {
        List<ListItem> list = new() { new(DEFAULT_PRESET, DEFAULT_PRESET, 0) { desc = DEFAULT_DESCRIPTION } };
        try
        {
            var files = AssetManager.ListDirectory(PRESET_SUBFOLDER, false, false, false)
                .Concat(Directory.GetFiles(PresetDirectory));

            //for (int i = 0; i < files.Length; i++)
            foreach(string file in files)
            {
                string dir = Path.GetDirectoryName(file);

                string name = Path.GetFileNameWithoutExtension(file); //remove the full path and extension

                //search for the file's original name (AssetManager removes the capitalization :( )
                foreach (string f in Directory.EnumerateFiles(dir))
                {
                    string f2 = Path.GetFileNameWithoutExtension(f);
                    if (f2.Equals(name, StringComparison.InvariantCultureIgnoreCase))
                    {
                        name = f2;
                        break;
                    }
                }

                ListItem it = new(file, name, list.Count);
                try //find description:
                {
                    string line = File.ReadLines(file).First(); //check the first line
                    string prefix = PRESET_DESCRIPTION_KEY + PRESET_SEPARATOR;
                    if (line.StartsWith(prefix)) //for the PRESET_DESCRIPTION key
                        it.desc = line.Substring(prefix.Length).Replace("<LINE>", "\n"); //and use it as the description
                } catch { }

                list.Add(it);
            }
        }
        catch (Exception ex) { Plugin.Error(ex); }
        return list;
    }

    public void SavePreset(string name, string description = "")
    {
        try
        {
            string s = "";
            if (description != "")
            {
                s += PRESET_DESCRIPTION_KEY + PRESET_SEPARATOR + description.Replace("\n", "<LINE>") + '\n';
            }
            foreach (ConfigInfo info in ConfigInfos.Values)
            {
                s += info.config.key + PRESET_SEPARATOR + info.config.BoxedValue + '\n';
            }

            string path = AssetManager.ResolveFilePath(Path.Combine(PRESET_SUBFOLDER, name) + ".txt");
            if (!File.Exists(path)) //if creating a new file, do it in the config folder
                path = Path.Combine(PresetDirectory, name + ".txt");

            string fileName = Path.GetFileName(path);
            path = path.Substring(0, path.Length - fileName.Length) + name + ".txt"; //replace name so that it's not all lowercase
            File.WriteAllText(path, s); //this will usually save in the StreamingAssets folder

            Plugin.Log("Saved options preset: " + name);
        }
        catch (Exception ex) { Plugin.Error(ex); }
    }

    public void LoadPreset(string path)
    {
        try
        {
            if (path == DEFAULT_PRESET) //just reset everything to its default value
            {
                foreach (var kvp in ConfigInfos)
                {
                    ConfigurableBase c = kvp.Value.config;
                    c.BoxedValue = c.defaultValue;
                    c.BoundUIconfig?.ShowConfig();
                }
            }
            else //load the config info from the file
            {
                if (!File.Exists(path))
                {
                    Plugin.Error("Could not find preset file: " + path);
                    return;
                }
                string[] lines = File.ReadAllLines(path);
                for (int i = 0; i < lines.Length; i++)
                {
                    string l = lines[i];
                    try
                    {
                        if (l.Length < 3) continue; //not possibly a valid line
                        if (i == 0 && l == PRESET_DESCRIPTION_KEY) continue; //not a real config

                        int idx = l.IndexOf(PRESET_SEPARATOR);
                        if (idx < 0)
                        {
                            Plugin.Error("Error parsing the following preset line: " + l);
                            continue;
                        }

                        string key = l.Substring(0, idx); //everything up to SEPARATOR
                        string val = l.Substring(idx + 1); //everything after SEPARATOR
                        if (ConfigInfos.TryGetValue(key, out ConfigInfo info))
                        {
                            info.config.BoxedValue = val;
                            info.config.BoundUIconfig?.ShowConfig(); //update the UI component
                        }
                        else
                        {
                            Plugin.Error("Could not find config with the following key: " + key);
                        }
                    }
                    catch (Exception ex) { Plugin.Error($"Error with line: {l}  : {ex}"); }
                }
            }

            //we just set the configs, so now set the corresponding fields
            SetAllFields();

            Plugin.Log("Loaded options preset: " + path);

        }
        catch (Exception ex) { Plugin.Error(ex); }
    }
    #endregion

}
