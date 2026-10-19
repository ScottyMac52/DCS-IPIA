using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace DcsIpia.Presentation
{
    /// <summary>Offline, keyboard-accessible guidance for the shipped user workflows.</summary>
    public sealed class WorkflowHelpWindow : Window
    {
        /// <summary>Shows version, workflow guidance and attribution locations without network access.</summary>
        public WorkflowHelpWindow()
        {
            Title = "DCS-IPIA — Help · " + ApplicationVersion.Label;
            Width = 760; Height = 600; MinWidth = 500; MinHeight = 360;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            SetResourceReference(BackgroundProperty, "WindowBackgroundBrush");
            SetResourceReference(ForegroundProperty, "PrimaryTextBrush");
            var tabs = new TabControl { Margin = new Thickness(20) };
            var about = new StackPanel { Margin = new Thickness(20) };
            var badge = new Image { Source = ApplicationBranding.Badge, Width = 144, Height = 144, Margin = new Thickness(0, 0, 0, 12) };
            System.Windows.Automation.AutomationProperties.SetName(badge, "DCS-IPIA Flight Patch");
            about.Children.Add(badge);
            about.Children.Add(new TextBlock { Text = "DCS-IPIA", FontSize = 26, FontWeight = FontWeights.Bold, HorizontalAlignment = HorizontalAlignment.Center });
            about.Children.Add(new TextBlock { Text = ApplicationVersion.Label, Margin = new Thickness(0, 8, 0, 12), HorizontalAlignment = HorizontalAlignment.Center });
            about.Children.Add(new TextBlock { Text = "Organize your flight controls, assign aircraft commands and build DCS input profiles and kneeboards.",
                TextWrapping = TextWrapping.Wrap, TextAlignment = TextAlignment.Center });
            about.Children.Add(new TextBlock { Text = EditionAccess.Policy.Label, Margin = new Thickness(0, 12, 0, 0), TextWrapping = TextWrapping.Wrap, TextAlignment = TextAlignment.Center });
            tabs.Items.Add(new TabItem { Header = "_About", Content = new ScrollViewer { Content = about, VerticalScrollBarVisibility = ScrollBarVisibility.Auto } });
            tabs.Items.Add(Topic("_Getting started", """
                DCS-IPIA
                VERSION

                Edition availability: Free supports the reviewed Thrustmaster catalog with a fixed appearance. Additional hardware, live input testing and customization are reserved for Premium; availability and pricing TBA. Current signed Owner activation unlocks implemented features; customer Premium licensing/payment is not yet available. Generic hardware and interactive training/input-test walkthroughs below require Owner access. Free training is proposed, not yet implemented.

                Hardware → Discover devices / Rescan reads controllers. Review capabilities, select each physical unit and give repeated units distinct labels/roles before saving. Saved devices may be disconnected: saved selection is not proof of current connectivity. Review unsupported objects and changed capabilities explicitly.

                Generic controls work without a bundled photo: select the reviewed capability definition when creating a module, assign the device, and use the generated button/hat/axis layout. Test live inputs before building; ambiguous or unsupported controls need review.

                Open the module’s top feature tabs: Devices, Modifiers, Assignments, Input Test, Commands, Pages, Kneeboards and Documentation. Assign using the command button or drag/drop; right-click callouts for labels, clear and axis tuning. Modifier activators are reserved. Fit shows the complete drawing. Save to database, then use Build & export to Validate, snapshot or package saved inputs. Hardware drawings → Import draw.io creates a verified user revision with captured local images. Applying or updating it is explicit; saved modules retain their pinned revisions. Keep editable source and images in a full backup.

                Fly Now! deploys a saved module to the chosen Saved Games DCS folder. Review assignments and validation first. It automatically removes default axis assignments for unused controllers; verify the result in DCS. Clear removes recorded deployment files. Back up DCS inputs separately before real-game testing.
                """));
            tabs.Items.Add(Topic("_Recovery and profiles", """
                Full user-data backups include saved modules, inventory, drawing sources/images and preferences. Save or discard drafts and close editor windows first. Data & recovery → Back up user data creates and verifies an archive outside the data directory.

                Restore verifies the archive, retains the previous data and replaces the selected profile. Close and restart IPIA afterwards. Review restored deployment records before deploying. DCS files are not restored. A module snapshot instead restores one module without replacing other saved modules.

                If startup cannot open the database, use the recovery window or retain the original files and restore a known-good backup. Interrupted directory replacement is recovered on restart. Do not delete the retained original or recovery journal to bypass an error.

                Data & recovery → Test profiles and reset creates isolated clean profiles. You can also start IPIA with --test-profile NAME (letters, digits, hyphen or underscore). Test profiles disable DCS deployment and isolate setup, modules, drawings and app/appearance preferences. Splitter/disclosure/zoom preferences currently share the Windows-user-wide workspace-layout.json; training does not read or write it. Owner activation is account-wide and outside profile backups. Reset requires a verified backup. Use Production only for a reviewed real-DCS acceptance run.
                """));
            tabs.Items.Add(Topic("_Diagnostics and keys", """
                Help → Export support report (Ctrl+Shift+D) shows the entire report before saving it locally. It reads saved state, excludes personal names/paths/GUIDs, raw logs and artwork, and does not upload anything. Unreadable scopes remain null with a failure category and next step; they are not silently counted as empty. Numbered devices/modules are local to that report.

                Setup is offered once per profile; skipping leaves it available under Settings without repeated automatic popups. Rerun preserves completion and saved data.

                Error logs provide exception detail. Logging levels are configured by assembly in appsettings.json; see the shipped logging guide before changing them. Raw logs can contain personal paths and device names. Review them separately before sharing. A support report is evidence of saved state, not proof of live inputs or a successful in-DCS run.

                F1 opens this help. Alt opens the main menu; arrow keys navigate it. Tab/Shift+Tab move focus, Space/Enter activate buttons, Ctrl+Tab moves between help topics, and Escape closes help or the report preview. Ctrl+N creates a module, Ctrl+O opens the selection, Ctrl+Shift+B opens backup tools. Read-only text can be selected and copied.
                """));
            tabs.Items.Add(Topic("_Attribution", """
                Release notices are shipped in the notices folder beside the application. The distribution guide lists dependency metadata and bundled artwork/font provenance, and separates remaining licensing decisions from technical acceptance.

                Free Edition background: DoD photo by Petty Officer 3rd Class Eben Boothby, U.S. Navy (Released), DVIDS image 9214816. The appearance of U.S. Department of War (DoW) visual information does not imply or constitute DoW endorsement. Source and use guidance: notices/free-edition-background-PROVENANCE.md.

                Custom drawings and images remain user-owned inputs. Import only content you have permission to use and redistribute. Import validation does not establish ownership or grant rights; retain source/permission evidence before sharing a module snapshot or backup.

                The notices and provenance inventory do not certify commercial distribution approval. Clean-Windows, actual-device and in-DCS acceptance must be recorded against the exact release version and commit.
                """));
            var layout = new DockPanel();
            var banner = new Image { Source = ApplicationBranding.Banner, Height = 120, Stretch = System.Windows.Media.Stretch.Uniform,
                Margin = new Thickness(20, 16, 20, 0) };
            System.Windows.Automation.AutomationProperties.SetName(banner, "DCS-IPIA — Inputs, Profiles, Aircraft");
            DockPanel.SetDock(banner, Dock.Top);
            layout.Children.Add(banner);
            layout.Children.Add(tabs);
            Content = layout;
            PreviewKeyDown += (_, e) => { if (e.Key == Key.Escape) { Close(); e.Handled = true; } };
        }
        #region BEGIN Public
        #endregion END Public
        #region BEGIN private helpers
        private static TabItem Topic(string title, string text) => new()
        {
            Header = title,
            Content = new TextBox { Text = text.Replace("VERSION", ApplicationVersion.Label, StringComparison.Ordinal), IsReadOnly = true,
                AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Margin = new Thickness(12) }
        };
        #endregion END private helpers
    }
}

