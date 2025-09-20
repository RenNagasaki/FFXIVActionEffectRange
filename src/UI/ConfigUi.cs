﻿using ActionEffectRange.Actions.Data;
using Dalamud.Bindings.ImGui;
using System.Diagnostics;

namespace ActionEffectRange.UI
{
    public static class ConfigUi
    {
        private static readonly ActionBlacklistEditUI actionBlacklistEditUI = new();
        private static readonly AoETypeEditUi aoeTypeEditUI = new();
        private static readonly ConeAoEAngleEditUI coneAoEAngleEditUI = new();

        public static void Draw()
        {
            if (!InConfig) return;

            DrawMainConfigUi();

            DrawSubUIs();

            RefreshConfig();
        }

        private static void DrawMainConfigUi()
        {
            ImGui.SetNextWindowSize(new(500, 400), ImGuiCond.FirstUseEver);
            if (ImGui.Begin("ActionEffectRange: Configuration"))
            {
                ImGui.TreePush("");
                ImGui.Checkbox("Enable plugin", ref Config.Enabled);
                ImGui.TreePop();

                if (Config.Enabled)
                {
                    ImGui.NewLine();
                    ImGui.TreePush("");
                    ImGui.Checkbox("Enable in PvP zones", ref Config.EnabledPvP);
                    ImGui.TreePop();

                    ImGuiExt.SpacedSeparator();

                    ImGui.Text("Drawing Options");
                    ImGui.NewLine();
                    ImGui.TreePush("");
                    ImGui.Columns(2, "DrawingOptions", false);
                    ImGuiExt.CheckboxWithTooltip("Enable for beneficial actions", 
                        ref Config.DrawBeneficial,
                        "If enabled, will draw effect range for actions with beneficial effects, " +
                        "\n such as heals and buffs.");
                    if (Config.DrawBeneficial)
                    {
                        ImGui.Indent();
                        ImGui.Text("Colour: ");
                        ImGui.ColorEdit4("##BeneficialColour", ref Config.BeneficialColour);
                        ImGui.Unindent();
                    }
                    ImGui.NextColumn();
                    ImGuiExt.CheckboxWithTooltip("Enable for harmful actions",
                        ref Config.DrawHarmful,
                        "If enabled, will draw effect range for actions with harmful effects, " +
                        "\n such as attacks and debuffs.");
                    if (Config.DrawHarmful)
                    {
                        ImGui.Indent();
                        ImGui.Text("Colour: ");
                        ImGui.ColorEdit4("##HarmfulColour", ref Config.HarmfulColour);
                        ImGui.Unindent();
                    }
                    ImGui.Columns(1);
                    ImGui.NewLine();
                    ImGuiExt.CheckboxWithTooltip("Enable drawing for ACN/SMN/SCH pets' actions", 
                        ref Config.DrawACNPets,
                        "If enabled, will also draw effect range for actions used by your own pet." +
                        "\nAffects only Arcanist/Summoner/Scholar pets' AoE actions.");
                    ImGuiExt.CheckboxWithTooltip("Enable drawing for other summoned companions",
                        ref Config.DrawSummonedCompanions,
                        "If enabled, will also draw effect range for actions used by companions you summoned." +
                        "\nAffects actions by those companions that " +
                        "have interactable models (just like normal SMN/SCH pets) " +
                        "\n and are summoned when you are NOT Arcanist/Summoner/Scholar." +
                        "\nFor example, MCH's autoturrets.");
                    ImGuiExt.CheckboxWithTooltip("Enable drawing for ground-targeted actions", 
                        ref Config.DrawGT,
                        "If enabled, will also draw effect range for ground-targeted actions.");
                    ImGuiExt.CheckboxWithTooltip("Enable drawing for Special/Artillery actions", 
                        ref Config.DrawEx,
                        "If enabled, will also draw effect range for actions of category " +
                        $"\"{ActionData.GetActionCategoryName(Actions.Enums.ActionCategory.Special)}\" or " +
                        $"\"{ActionData.GetActionCategoryName(Actions.Enums.ActionCategory.Artillery)}\"." +
                        $"\n\nActions of these categories are generally available in certain contents/duties, " +
                        $"\nafter you mount something or transformed into something, etc." +
                        $"\n\nWarning: effect range drawing for these actions may be very inaccurate.");
                    ImGui.NewLine();

                    ImGui.Text("Actions with large effect range: ");
                    ImGui.Indent();
                    ImGui.Combo("##LargeDrawOpt", ref Config.LargeDrawOpt, 
                        Configuration.LargeDrawOptions, 
                        Configuration.LargeDrawOptions.Length);
                    ImGuiExt.SetTooltipIfHovered(
                        $"If set to any option other than \"{Configuration.LargeDrawOptions[0]}\", " +
                        "AoEs with effect range at least " +
                        "\n as large as the number specified below will be drawn" +
                        "\n (or not drawn at all) according to the set option." +
                        "\n\nThis only applies to Circle or Donut AoEs (including Ground-targeted ones)." +
                        "\nOther types of AoEs are not affected by this setting.");
                    ImGui.Unindent();
                    if (Config.LargeDrawOpt > 0)
                    {
                        ImGuiExt.InputIntWithTooltip("Apply to actions with effect range >= ", 
                            ref Config.LargeThreshold, 1, 1, 5, 55, 0, 80,
                            "The setting will be applied to actions with at least the specified effect range." +
                            "\nFor example, if set to 15, AoE such as Medica and Medica II" +
                            "\n will be affected by the setting, but not Cure III.");
                    }
                    ImGui.TreePop();

                    ImGuiExt.SpacedSeparator();

                    ImGui.Text("Style options");
                    ImGui.NewLine();
                    ImGui.TreePush("");
                    ImGui.Columns(2, "StyleOptions", false);
                    ImGui.Checkbox("Draw outline (outer ring)", ref Config.OuterRing);
                    if (Config.OuterRing)
                    {
                        ImGuiExt.DragIntWithTooltip("Thickness: ", 
                            ref Config.Thickness, 1, 1, 50, 60, null);
                        if (Config.Thickness < 1) Config.Thickness = 1;
                        if (Config.Thickness > 50) Config.Thickness = 50;
                    }
                    ImGui.NextColumn();
                    ImGui.Checkbox("Fill colour", ref Config.Filled);
                    if (Config.Filled)
                    {
                        ImGuiExt.DragFloatWithTooltip("Opacity: ", 
                            ref Config.FillAlpha, .01f, 0, 1, "%.2f", 60, null);
                        if (Config.FillAlpha < 0) Config.FillAlpha = 0;
                        if (Config.FillAlpha > 1) Config.FillAlpha = 1;
                    }
                    ImGui.Columns(1);
                    ImGui.NewLine();
                    ImGuiExt.DragIntWithTooltip("Smoothness: ", 
                        ref Config.NumSegments, 10, 40, 500, 100,
                        "The larger number, the smoothier");
                    if (Config.NumSegments < 40) Config.NumSegments = 40;
                    if (Config.NumSegments > 500) Config.NumSegments = 500;
                    ImGui.TreePop();

                    ImGuiExt.SpacedSeparator();

                    ImGui.TreePush("");
                    ImGuiExt.DragFloatWithTooltip("Delay before drawing (sec): ", 
                        ref Config.DrawDelay, .1f, 0, 2, "%.3f", 80,
                        "Delay (in seconds) to wait immediately after using an action " +
                        "before drawing the effect range.");
                    ImGuiExt.DragFloatWithTooltip("Remove drawing after time (sec): ", 
                        ref Config.PersistSeconds, .1f, .1f, 5, "%.3f", 80,
                        "Allow the effect range drawn to last for the given time " +
                        "(in seconds) before erased from screen.");
                    ImGui.TreePop();

                    ImGuiExt.SpacedSeparator();

                    ImGui.TreePush("");
                    ImGuiExt.CheckboxWithTooltip("Enable drawing during casting",
                        ref Config.DrawWhenCasting,
                        "If enabled, will also draw effect range when you are casting an AoE action.\n" +
                        "Currently this only works in PvE areas.");
                    if (Config.DrawWhenCasting)
                    {
                        ImGui.NewLine();
                        ImGui.Text("Colour: ");
                        ImGui.SameLine();
                        ImGui.ColorEdit4("##DrawWhenCastingColour", 
                            ref Config.DrawWhenCastingColour);
                        ImGuiExt.CheckboxWithTooltip("Draw until casting ends",
                            ref Config.DrawWhenCastingUntilCastEnd,
                            "If enabled, drawing of the casting action will last " +
                            "until the casting is finished or cancelled.\n" +
                            "Otherwise it will be removed after the duration set above.");
                    }
                    ImGui.TreePop();

                    ImGuiExt.SpacedSeparator();

                    ImGuiExt.BulletTextWrappedWithHelpMarker("Advanced Customisation Options", 
                        "Use these customisation options to control the drawing for specific actions.\n\n" +
                        "This is mainly for providing a temporary fix to incorrect drawing, " +
                        "such as incorrect Cone AoE angles.\n" +
                        "Usually you don't need to care about any of these customisations.");
                    ImGui.NewLine();
                    ImGui.TreePush("");
                    if (ImGui.Button("Edit Action Blacklist"))
                        actionBlacklistEditUI.OpenUI();
                    // TODO: this is too confusing and useless rn, unless
                    // there're customisations for other data such as effect range after types being overriden.
                    //if (ImGui.Button("Customise AoE Types"))
                    //    aoeTypeEditUI.OpenUI();
                    if (ImGui.Button("Customise Cone AoE Drawing"))
                        coneAoEAngleEditUI.OpenUI();
                    ImGui.TreePop();

                    ImGuiExt.SpacedSeparator();

                    ImGui.TreePush("");
                    ImGui.Checkbox($"[DEBUG] Log debug info to Dalamud Console", ref Config.LogDebug);
                    ImGui.NewLine();
                    ImGui.Checkbox("Show Sponsor/Support button", ref Config.ShowSponsor);
                    if (Config.ShowSponsor)
                    {
                        ImGui.Indent();
                        ImGui.PushStyleColor(ImGuiCol.Button, 0xFF000000 | 0x005E5BFF);
                        ImGui.PushStyleColor(ImGuiCol.ButtonActive, 0xDD000000 | 0x005E5BFF);
                        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, 0xAA000000 | 0x005E5BFF);
                        if (ImGuiExt.Button("Buy Yomishino a Coffee", 
                            "You can support me and buy me a coffee if you want.\n" +
                            "(Will open external link to Ko-fi in your browser)"))
                        {
                            Process.Start(new ProcessStartInfo
                            {
                                FileName = "https://ko-fi.com/yomishino",
                                UseShellExecute = true
                            });
                        }
                        ImGui.PopStyleColor(3);
                        ImGui.Unindent();
                    }
                    ImGui.TreePop();
                }

                ImGuiExt.SpacedSeparator();

                if (ImGui.Button("Save & Close"))
                {
                    CloseSubUIs();
                    ActionData.SaveCustomisedData();
                    Config.Save();
                    InConfig = false;
                }

                ImGui.End();
            }
        }

        private static void DrawSubUIs()
        {
            actionBlacklistEditUI.Draw();
            aoeTypeEditUI.Draw();
            coneAoEAngleEditUI.Draw();
        }

        private static void CloseSubUIs()
        {
            actionBlacklistEditUI.CloseUI();
            aoeTypeEditUI.CloseUI();
            coneAoEAngleEditUI.CloseUI();
        }

    }
}
