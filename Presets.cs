using Menu.Remix.MixedUI;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TrueParallax;

public partial class Options
{
    #region UI
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
            new OpLabel(100, 500, "Load Preset", true),
            presetsBox = new(new Configurable<string>(""), new(100, 450), 200, GetAllPresets(), 10),
            loadButton = new(new(400, 450), new UnityEngine.Vector2(150, 50), "Load Preset") { description = "Overrides all your current configs with the selected preset."},

            new OpLabel(100, 300, "Save Preset", true),
            saveNameBox = new(new Configurable<string>("My Preset"), new(100, 250), 200) { description = "The name of the preset you would like to save. Using the name of an existing preset will overwrite it."},
            saveButton = new(new(400, 250), new UnityEngine.Vector2(150, 50), "Save Preset") { description = "Saves your current options as a preset, overwriting any former presets with that name."},

            fileButton = new(new(200, 100), new UnityEngine.Vector2(150, 50), "View Presets Folder") { description = "View your presets in the file explorer."}
            );

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
        saveNameBox.value = value;
    }

    private void LoadButton_OnPressDone(UIfocusable trigger)
    {
        trigger.Menu.PlaySound(SoundID.MENU_Button_Standard_Button_Pressed);
        LoadPreset(presetsBox.value);
    }

    private void SaveButton_OnPressDone(UIfocusable trigger)
    {
        trigger.Menu.PlaySound(SoundID.MENU_Button_Standard_Button_Pressed);
        SavePreset(saveNameBox.value);
    }

    private void FileButton_OnPressDone(UIfocusable trigger)
    {
        trigger.Menu.PlaySound(SoundID.MENU_Button_Standard_Button_Pressed);

        try
        {
            string path = Path.Combine(RWCustom.Custom.RootFolderDirectory(), PRESET_SUBFOLDER);
            if (!Directory.Exists(path))
                Directory.CreateDirectory(path); //make sure there's actually a folder to open

            System.Diagnostics.Process.Start(path);
        }
        catch (Exception ex) { Plugin.Error(ex); }
    }

    #endregion

    #region Files
    public const string PRESET_SUBFOLDER = "ParallaxPresets";
    public const char PRESET_SEPARATOR = '=';

    public string[] GetAllPresets()
    {
        try
        {
            string[] files = AssetManager.ListDirectory(PRESET_SUBFOLDER, false, false, false);
            //string[] files = Directory.GetFiles(Path.Combine(Plugin.PluginPath, PRESET_SUBFOLDER));
            for (int i = 0; i < files.Length; i++)
            {
                files[i] = Path.GetFileName(files[i]); //remove the full path
            }
            if (files.Length > 0)
                return files;
        }
        catch (Exception ex) { Plugin.Error(ex); }
        return new string[1] {""};
    }

    public void SavePreset(string name)
    {
        try
        {
            string s = "";
            foreach (ConfigInfo info in ConfigInfos.Values)
            {
                s += info.config.key + PRESET_SEPARATOR + info.config.BoxedValue + '\n';
            }
            //File.WriteAllText(Path.Combine(Plugin.PluginPath, PRESET_SUBFOLDER, name) + ".txt", s);
            File.WriteAllText(AssetManager.ResolveFilePath(Path.Combine(PRESET_SUBFOLDER, name) + ".txt"), s); //this will usually save in the StreamingAssets folder

            Plugin.Log("Saved options preset: " + name);
        }
        catch (Exception ex) { Plugin.Error(ex); }
    }

    public void LoadPreset(string name)
    {
        try
        {
            //string path = AssetManager.ResolveFilePath(Path.Combine(PRESET_SUBFOLDER, name) + ".txt");
            string path = Path.Combine(Plugin.PluginPath, PRESET_SUBFOLDER, name) + ".txt";
            if (!File.Exists(path))
            {
                Plugin.Error("Could not find preset file: " + path);
                return;
            }
            string[] lines = File.ReadAllLines(path);
            foreach (string l in lines)
            {
                try
                {
                    if (l.Length < 3) continue; //not possibly a valid line

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
                    }
                    else
                    {
                        Plugin.Error("Could not find config with the following key: " + key);
                    }
                }
                catch (Exception ex) { Plugin.Error($"Error with line: {l}  : {ex}"); }
            }

            //we just set the configs, so now set the corresponding fields
            SetAllFields();

            Plugin.Log("Loaded options preset: " + name);

        }
        catch (Exception ex) { Plugin.Error(ex); }
    }
    #endregion

}
