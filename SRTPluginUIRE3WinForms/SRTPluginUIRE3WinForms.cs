using SRTPluginBase;
using SRTPluginProviderRE3.Structs;
using SRTPluginProviderRE3.Structs.GameStructs;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace SRTPluginUIRE3WinForms
{
    public class SRTPluginUIRE3WinForms : PluginBase, IPluginUI
    {
        internal static PluginInfo _Info = new PluginInfo();
        public override IPluginInfo Info => _Info;
        public string RequiredProvider => "SRTPluginProviderRE3";

        private IPluginHostDelegates hostDelegates;
        private ApplicationContext applicationContext;
        private Task applicationTask;
        public static ContextMenuStrip contextMenuStrip;

        private bool oneTimeInit = false;

        [STAThread]
        public override int Startup(IPluginHostDelegates hostDelegates)
        {
            this.hostDelegates = hostDelegates;
            Program.config = LoadConfiguration<PluginConfiguration>();

            if (!oneTimeInit)
            {
                // Must be before any rendering happens, including creation of ContextMenuStrip in Program class.
                Application.SetHighDpiMode(HighDpiMode.DpiUnaware);
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                oneTimeInit = true;
            }

            // Context menu.
            contextMenuStrip = new ContextMenuStrip();
            contextMenuStrip.Items.Add(new ToolStripMenuItem("Options", null, (object sender, EventArgs e) =>
            {
                using (OptionsUI optionsForm = new OptionsUI())
                    optionsForm.ShowDialog();
            }));
            contextMenuStrip.Items.Add(new ToolStripSeparator());
            contextMenuStrip.Items.Add(new ToolStripMenuItem("Exit", null, (object sender, EventArgs e) =>
            {
                // Call Application.Exit() within the context of the Form.
                applicationContext?.MainForm?.Invoke(new Action(() => Application.Exit()));
            }));

            // Call the legacy code initialization.
            Program.Main(new string[0]);

            // Create and start the form.
            applicationContext = new ApplicationContext(new MainUI());
            applicationTask = Task.Run(() =>
            {
                Application.Run(applicationContext);
            }).ContinueWith((Task t) =>
            {
                Shutdown();
            });

            return 0;
        }

        public override int Shutdown()
        {
            SaveConfiguration(Program.config);

            // Clean up the context.
            if (applicationContext != null)
            {
                // Clean up the form.
                if (applicationContext.MainForm != null)
                {
                    applicationContext.MainForm.Invoke(new Action(() =>
                    {
                        applicationContext.MainForm.Close();
                        applicationContext.MainForm.Dispose();
                    }));
                    applicationContext.MainForm = null;
                }

                applicationContext.Dispose();
                applicationContext = null;
            }

            return 0;
        }

        public int ReceiveData(object gameMemory)
        {
            if (applicationContext?.MainForm != null)
                ((MainUI)applicationContext?.MainForm).ReceiveData(gameMemory);
            return 0;
        }
    }

    public static class Program
    {
        public static PluginConfiguration config;

        public static readonly string srtVersion = string.Format("v{0}.{1}.{2}.{3}", SRTPluginUIRE3WinForms._Info.VersionMajor, SRTPluginUIRE3WinForms._Info.VersionMinor, SRTPluginUIRE3WinForms._Info.VersionBuild, SRTPluginUIRE3WinForms._Info.VersionRevision);
        public static readonly string srtTitle = string.Format("RE3(2020) SRT - {0}", srtVersion);

        public static int INV_SLOT_WIDTH;
        public static int INV_SLOT_HEIGHT;

        public static IReadOnlyDictionary<ItemID, System.Drawing.Rectangle> ItemToImageTranslation;
        public static IReadOnlyDictionary<Weapon, System.Drawing.Rectangle> WeaponToImageTranslation;

        public static IReadOnlyDictionary<ItemID, System.Drawing.TextureBrush> ItemToImageBrush;
        public static IReadOnlyDictionary<Weapon, System.Drawing.TextureBrush> WeaponToImageBrush;
        public static System.Drawing.TextureBrush ErrorToImageBrush;

        public static void Main(string[] args) // NOT THE REAL ENTRYPOINT. THIS IS A PLACEHOLDER FOR LEGACY CODE TO BE PORTED.
        {
            try
            {
                // Handle command-line parameters.
                //foreach (string arg in args)
                //{
                //    if (arg.Equals("--Help", StringComparison.InvariantCultureIgnoreCase))
                //    {
                //        StringBuilder message = new StringBuilder("Command-line arguments:\r\n\r\n");
                //        message.AppendFormat("{0}\r\n\t{1}\r\n\r\n", "--No-Titlebar", "Hide the titlebar and window frame.");
                //        message.AppendFormat("{0}\r\n\t{1}\r\n\r\n", "--Always-On-Top", "Always appear on top of other windows.");
                //        message.AppendFormat("{0}\r\n\t{1}\r\n\r\n", "--Transparent", "Make the background transparent.");
                //        message.AppendFormat("{0}\r\n\t{1}\r\n\r\n", "--ScalingFactor=n", "Set the inventory slot scaling factor on a scale of 0.0 to 1.0. Default: 0.75 (75%)");
                //        message.AppendFormat("{0}\r\n\t{1}\r\n\r\n", "--NoInventory", "Disables the inventory display.");
                //        message.AppendFormat("{0}\r\n\t{1}\r\n\r\n", "--Debug", "Debug mode.");

                //        MessageBox.Show(null, message.ToString().Trim(), string.Empty, MessageBoxButtons.OK, MessageBoxIcon.Information);
                //        Environment.Exit(0);
                //    }

                //    if (arg.Equals("--No-Titlebar", StringComparison.InvariantCultureIgnoreCase))
                //        programSpecialOptions.Flags |= ProgramFlags.NoTitleBar;

                //    if (arg.Equals("--Always-On-Top", StringComparison.InvariantCultureIgnoreCase))
                //        programSpecialOptions.Flags |= ProgramFlags.AlwaysOnTop;

                //    if (arg.Equals("--Transparent", StringComparison.InvariantCultureIgnoreCase))
                //        programSpecialOptions.Flags |= ProgramFlags.Transparent;

                //    if (arg.Equals("--NoInventory", StringComparison.InvariantCultureIgnoreCase))
                //        programSpecialOptions.Flags |= ProgramFlags.NoInventory;

                //    if (arg.StartsWith("--ScalingFactor=", StringComparison.InvariantCultureIgnoreCase))
                //        if (!double.TryParse(arg.Split(new char[1] { '=' }, 2, StringSplitOptions.None)[1], out programSpecialOptions.ScalingFactor))
                //            programSpecialOptions.ScalingFactor = 0.75d; // Default scaling factor for the inventory images. If we fail to process the user input, ensure this gets set to the default value just in case.

                //    if (arg.Equals("--Debug", StringComparison.InvariantCultureIgnoreCase))
                //        programSpecialOptions.Flags |= ProgramFlags.Debug;
                //}

                // Set item slot sizes after scaling is determined.
                INV_SLOT_WIDTH = (int)Math.Round(112d * config.ScalingFactor, MidpointRounding.AwayFromZero); // Individual inventory slot width.
                INV_SLOT_HEIGHT = (int)Math.Round(112d * config.ScalingFactor, MidpointRounding.AwayFromZero); // Individual inventory slot height.

                GenerateClipping();
            }
            catch (Exception ex)
            {
                FailFast(string.Format("[{0}] An unhandled exception has occurred. Please see below for details.\r\n\r\n[{1}] {2}\r\n{3}.", srtVersion, ex.GetType().ToString(), ex.Message, ex.StackTrace), ex);
            }
        }

        public static void FailFast(string message, Exception ex)
        {
            ShowError(message);
            Environment.FailFast(message, ex);
        }

        public static void ShowError(string message)
        {
            MessageBox.Show(message, srtTitle, MessageBoxButtons.OK, MessageBoxIcon.Error, MessageBoxDefaultButton.Button1);
        }

        public static string GetExceptionMessage(Exception ex) => string.Format("[{0}] An unhandled exception has occurred. Please see below for details.\r\n\r\n[{1}] {2}\r\n{3}.", srtVersion, ex.GetType().ToString(), ex.Message, ex.StackTrace);

        public static void GenerateClipping()
        {
            int itemColumnInc = -1;
            int itemRowInc = -1;
            ItemToImageTranslation = new Dictionary<ItemID, System.Drawing.Rectangle>()
            {
                { ItemID.None, new System.Drawing.Rectangle(Program.INV_SLOT_WIDTH * 0, Program.INV_SLOT_HEIGHT * 8, Program.INV_SLOT_WIDTH, Program.INV_SLOT_HEIGHT) },

                // Row 0.
                { ItemID.First_Aid_Spray, new System.Drawing.Rectangle(Program.INV_SLOT_WIDTH * (itemColumnInc = 0), Program.INV_SLOT_HEIGHT * ++itemRowInc, Program.INV_SLOT_WIDTH, Program.INV_SLOT_HEIGHT) },
                { ItemID.Green_Herb, new System.Drawing.Rectangle(Program.INV_SLOT_WIDTH * ++itemColumnInc, Program.INV_SLOT_HEIGHT * itemRowInc, Program.INV_SLOT_WIDTH, Program.INV_SLOT_HEIGHT) },
                { ItemID.Red_Herb, new System.Drawing.Rectangle(Program.INV_SLOT_WIDTH * ++itemColumnInc, Program.INV_SLOT_HEIGHT * itemRowInc, Program.INV_SLOT_WIDTH, Program.INV_SLOT_HEIGHT) },
                { ItemID.Mixed_Herb_GG, new System.Drawing.Rectangle(Program.INV_SLOT_WIDTH * ++itemColumnInc, Program.INV_SLOT_HEIGHT * itemRowInc, Program.INV_SLOT_WIDTH, Program.INV_SLOT_HEIGHT) },
                { ItemID.Mixed_Herb_GR, new System.Drawing.Rectangle(Program.INV_SLOT_WIDTH * ++itemColumnInc, Program.INV_SLOT_HEIGHT * itemRowInc, Program.INV_SLOT_WIDTH, Program.INV_SLOT_HEIGHT) },
                { ItemID.Mixed_Herb_GGG, new System.Drawing.Rectangle(Program.INV_SLOT_WIDTH * ++itemColumnInc, Program.INV_SLOT_HEIGHT * itemRowInc, Program.INV_SLOT_WIDTH, Program.INV_SLOT_HEIGHT) },
                { ItemID.Green_Herb2, new System.Drawing.Rectangle(Program.INV_SLOT_WIDTH * ++itemColumnInc, Program.INV_SLOT_HEIGHT * itemRowInc, Program.INV_SLOT_WIDTH, Program.INV_SLOT_HEIGHT) },
                { ItemID.Red_Herb2, new System.Drawing.Rectangle(Program.INV_SLOT_WIDTH * ++itemColumnInc, Program.INV_SLOT_HEIGHT * itemRowInc, Program.INV_SLOT_WIDTH, Program.INV_SLOT_HEIGHT) },

                // Row 1.
                { ItemID.Handgun_Ammo, new System.Drawing.Rectangle(Program.INV_SLOT_WIDTH * (itemColumnInc = 0), Program.INV_SLOT_HEIGHT * ++itemRowInc, Program.INV_SLOT_WIDTH, Program.INV_SLOT_HEIGHT) },
                { ItemID.Shotgun_Shells, new System.Drawing.Rectangle(Program.INV_SLOT_WIDTH * ++itemColumnInc, Program.INV_SLOT_HEIGHT * itemRowInc, Program.INV_SLOT_WIDTH, Program.INV_SLOT_HEIGHT) },
                { ItemID.Assault_Rifle_Ammo, new System.Drawing.Rectangle(Program.INV_SLOT_WIDTH * ++itemColumnInc, Program.INV_SLOT_HEIGHT * itemRowInc, Program.INV_SLOT_WIDTH, Program.INV_SLOT_HEIGHT) },
                { ItemID.MAG_Ammo, new System.Drawing.Rectangle(Program.INV_SLOT_WIDTH * ++itemColumnInc, Program.INV_SLOT_HEIGHT * itemRowInc, Program.INV_SLOT_WIDTH, Program.INV_SLOT_HEIGHT) },
                { ItemID.Acid_Rounds, new System.Drawing.Rectangle(Program.INV_SLOT_WIDTH * ++itemColumnInc, Program.INV_SLOT_HEIGHT * itemRowInc, Program.INV_SLOT_WIDTH, Program.INV_SLOT_HEIGHT) },
                { ItemID.Flame_Rounds, new System.Drawing.Rectangle(Program.INV_SLOT_WIDTH * ++itemColumnInc, Program.INV_SLOT_HEIGHT * itemRowInc, Program.INV_SLOT_WIDTH, Program.INV_SLOT_HEIGHT) },
                { ItemID.Explosive_Rounds, new System.Drawing.Rectangle(Program.INV_SLOT_WIDTH * ++itemColumnInc, Program.INV_SLOT_HEIGHT * itemRowInc, Program.INV_SLOT_WIDTH, Program.INV_SLOT_HEIGHT) },
                { ItemID.Mine_Rounds, new System.Drawing.Rectangle(Program.INV_SLOT_WIDTH * ++itemColumnInc, Program.INV_SLOT_HEIGHT * itemRowInc, Program.INV_SLOT_WIDTH, Program.INV_SLOT_HEIGHT) },
                { ItemID.Gunpowder, new System.Drawing.Rectangle(Program.INV_SLOT_WIDTH * ++itemColumnInc, Program.INV_SLOT_HEIGHT * itemRowInc, Program.INV_SLOT_WIDTH, Program.INV_SLOT_HEIGHT) },
                { ItemID.HighGrade_Gunpowder, new System.Drawing.Rectangle(Program.INV_SLOT_WIDTH * ++itemColumnInc, Program.INV_SLOT_HEIGHT * itemRowInc, Program.INV_SLOT_WIDTH, Program.INV_SLOT_HEIGHT) },
                { ItemID.Explosive_A, new System.Drawing.Rectangle(Program.INV_SLOT_WIDTH * ++itemColumnInc, Program.INV_SLOT_HEIGHT * itemRowInc, Program.INV_SLOT_WIDTH, Program.INV_SLOT_HEIGHT) },
                { ItemID.Explosive_B, new System.Drawing.Rectangle(Program.INV_SLOT_WIDTH * ++itemColumnInc, Program.INV_SLOT_HEIGHT * itemRowInc, Program.INV_SLOT_WIDTH, Program.INV_SLOT_HEIGHT) },

                // Row 2.
                { ItemID.Moderator_Handgun, new System.Drawing.Rectangle(Program.INV_SLOT_WIDTH * (itemColumnInc = 0), Program.INV_SLOT_HEIGHT * ++itemRowInc, Program.INV_SLOT_WIDTH, Program.INV_SLOT_HEIGHT) },
                { ItemID.Dot_Sight_Handgun, new System.Drawing.Rectangle(Program.INV_SLOT_WIDTH * ++itemColumnInc, Program.INV_SLOT_HEIGHT * itemRowInc, Program.INV_SLOT_WIDTH, Program.INV_SLOT_HEIGHT) },
                { ItemID.Extended_Magazine_Handgun, new System.Drawing.Rectangle(Program.INV_SLOT_WIDTH * ++itemColumnInc, Program.INV_SLOT_HEIGHT * itemRowInc, Program.INV_SLOT_WIDTH, Program.INV_SLOT_HEIGHT) },
                { ItemID.SemiAuto_Barrel_Shotgun, new System.Drawing.Rectangle(Program.INV_SLOT_WIDTH * ++itemColumnInc, Program.INV_SLOT_HEIGHT * itemRowInc, Program.INV_SLOT_WIDTH, Program.INV_SLOT_HEIGHT) },
                { ItemID.Tactical_Stock_Shotgun, new System.Drawing.Rectangle(Program.INV_SLOT_WIDTH * ++itemColumnInc, Program.INV_SLOT_HEIGHT * itemRowInc, Program.INV_SLOT_WIDTH, Program.INV_SLOT_HEIGHT) },
                { ItemID.Shell_Holder_Shotgun, new System.Drawing.Rectangle(Program.INV_SLOT_WIDTH * ++itemColumnInc, Program.INV_SLOT_HEIGHT * itemRowInc, Program.INV_SLOT_WIDTH, Program.INV_SLOT_HEIGHT) },
                { ItemID.Scope_Assault_Rifle, new System.Drawing.Rectangle(Program.INV_SLOT_WIDTH * ++itemColumnInc, Program.INV_SLOT_HEIGHT * itemRowInc, Program.INV_SLOT_WIDTH, Program.INV_SLOT_HEIGHT) },
                { ItemID.Dual_Magazine_Assault_Rifle, new System.Drawing.Rectangle(Program.INV_SLOT_WIDTH * ++itemColumnInc, Program.INV_SLOT_HEIGHT * itemRowInc, Program.INV_SLOT_WIDTH, Program.INV_SLOT_HEIGHT) },
                { ItemID.Tactical_Grip_Assault_Rifle, new System.Drawing.Rectangle(Program.INV_SLOT_WIDTH * ++itemColumnInc, Program.INV_SLOT_HEIGHT * itemRowInc, Program.INV_SLOT_WIDTH, Program.INV_SLOT_HEIGHT) },
                { ItemID.Extended_Barrel_MAG, new System.Drawing.Rectangle(Program.INV_SLOT_WIDTH * ++itemColumnInc, Program.INV_SLOT_HEIGHT * itemRowInc, Program.INV_SLOT_WIDTH, Program.INV_SLOT_HEIGHT) },
                { ItemID.Supply_Crate_Acid_Rounds, new System.Drawing.Rectangle(Program.INV_SLOT_WIDTH * ++itemColumnInc, Program.INV_SLOT_HEIGHT * itemRowInc, Program.INV_SLOT_WIDTH, Program.INV_SLOT_HEIGHT) },
                { ItemID.Supply_Crate_Extended_Barrel_MAG, new System.Drawing.Rectangle(Program.INV_SLOT_WIDTH * ++itemColumnInc, Program.INV_SLOT_HEIGHT * itemRowInc, Program.INV_SLOT_WIDTH, Program.INV_SLOT_HEIGHT) },
                { ItemID.Supply_Crate_Extended_Magazine_Handgun, new System.Drawing.Rectangle(Program.INV_SLOT_WIDTH * ++itemColumnInc, Program.INV_SLOT_HEIGHT * itemRowInc, Program.INV_SLOT_WIDTH, Program.INV_SLOT_HEIGHT) },
                { ItemID.Supply_Crate_Flame_Rounds, new System.Drawing.Rectangle(Program.INV_SLOT_WIDTH * ++itemColumnInc, Program.INV_SLOT_HEIGHT * itemRowInc, Program.INV_SLOT_WIDTH, Program.INV_SLOT_HEIGHT) },
                { ItemID.Supply_Crate_Moderator_Handgun, new System.Drawing.Rectangle(Program.INV_SLOT_WIDTH * ++itemColumnInc, Program.INV_SLOT_HEIGHT * itemRowInc, Program.INV_SLOT_WIDTH, Program.INV_SLOT_HEIGHT) },
                { ItemID.Supply_Crate_Shotgun_Shells, new System.Drawing.Rectangle(Program.INV_SLOT_WIDTH * ++itemColumnInc, Program.INV_SLOT_HEIGHT * itemRowInc, Program.INV_SLOT_WIDTH, Program.INV_SLOT_HEIGHT) },

                //Row 3.
                { ItemID.Battery, new System.Drawing.Rectangle(Program.INV_SLOT_WIDTH * (itemColumnInc = 0), Program.INV_SLOT_HEIGHT * ++itemRowInc, Program.INV_SLOT_WIDTH, Program.INV_SLOT_HEIGHT) },
                { ItemID.Safety_Deposit_Key, new System.Drawing.Rectangle(Program.INV_SLOT_WIDTH * ++itemColumnInc, Program.INV_SLOT_HEIGHT * itemRowInc, Program.INV_SLOT_WIDTH, Program.INV_SLOT_HEIGHT) },
                { ItemID.Detonator_No_Battery, new System.Drawing.Rectangle(Program.INV_SLOT_WIDTH * ++itemColumnInc, Program.INV_SLOT_HEIGHT * itemRowInc, Program.INV_SLOT_WIDTH, Program.INV_SLOT_HEIGHT) },
                { ItemID.Brads_ID_Card, new System.Drawing.Rectangle(Program.INV_SLOT_WIDTH * ++itemColumnInc, Program.INV_SLOT_HEIGHT * itemRowInc, Program.INV_SLOT_WIDTH, Program.INV_SLOT_HEIGHT) },
                { ItemID.Detonator, new System.Drawing.Rectangle(Program.INV_SLOT_WIDTH * ++itemColumnInc, Program.INV_SLOT_HEIGHT * itemRowInc, Program.INV_SLOT_WIDTH, Program.INV_SLOT_HEIGHT) },
                { ItemID.Detonator2, new System.Drawing.Rectangle(Program.INV_SLOT_WIDTH * itemColumnInc, Program.INV_SLOT_HEIGHT * itemRowInc, Program.INV_SLOT_WIDTH, Program.INV_SLOT_HEIGHT) },
                { ItemID.Lock_Pick, new System.Drawing.Rectangle(Program.INV_SLOT_WIDTH * (itemColumnInc = 8), Program.INV_SLOT_HEIGHT * itemRowInc, Program.INV_SLOT_WIDTH, Program.INV_SLOT_HEIGHT) },
                { ItemID.Bolt_Cutters, new System.Drawing.Rectangle(Program.INV_SLOT_WIDTH * ++itemColumnInc, Program.INV_SLOT_HEIGHT * itemRowInc, Program.INV_SLOT_WIDTH, Program.INV_SLOT_HEIGHT) },

                // Row 4.
                { ItemID.Fire_Hose, new System.Drawing.Rectangle(Program.INV_SLOT_WIDTH * (itemColumnInc = 0), Program.INV_SLOT_HEIGHT * ++itemRowInc, Program.INV_SLOT_WIDTH, Program.INV_SLOT_HEIGHT) },
                { ItemID.Fire_Hose2, new System.Drawing.Rectangle(Program.INV_SLOT_WIDTH * itemColumnInc, Program.INV_SLOT_HEIGHT * itemRowInc, Program.INV_SLOT_WIDTH, Program.INV_SLOT_HEIGHT) },
                { ItemID.Kendos_Gate_Key, new System.Drawing.Rectangle(Program.INV_SLOT_WIDTH * ++itemColumnInc, Program.INV_SLOT_HEIGHT * itemRowInc, Program.INV_SLOT_WIDTH, Program.INV_SLOT_HEIGHT) },
                { ItemID.Battery_Pack, new System.Drawing.Rectangle(Program.INV_SLOT_WIDTH * ++itemColumnInc, Program.INV_SLOT_HEIGHT * itemRowInc, Program.INV_SLOT_WIDTH * 2, Program.INV_SLOT_HEIGHT) },
                { ItemID.Case_Lock_Pick, new System.Drawing.Rectangle(Program.INV_SLOT_WIDTH * (itemColumnInc = 4), Program.INV_SLOT_HEIGHT * itemRowInc, Program.INV_SLOT_WIDTH, Program.INV_SLOT_HEIGHT) },
                { ItemID.Green_Jewel, new System.Drawing.Rectangle(Program.INV_SLOT_WIDTH * ++itemColumnInc, Program.INV_SLOT_HEIGHT * itemRowInc, Program.INV_SLOT_WIDTH, Program.INV_SLOT_HEIGHT) },
                { ItemID.Blue_Jewel, new System.Drawing.Rectangle(Program.INV_SLOT_WIDTH * ++itemColumnInc, Program.INV_SLOT_HEIGHT * itemRowInc, Program.INV_SLOT_WIDTH, Program.INV_SLOT_HEIGHT) },
                { ItemID.Red_Jewel, new System.Drawing.Rectangle(Program.INV_SLOT_WIDTH * ++itemColumnInc, Program.INV_SLOT_HEIGHT * itemRowInc, Program.INV_SLOT_WIDTH, Program.INV_SLOT_HEIGHT) },
                { ItemID.Fancy_Box_Green_Jewel, new System.Drawing.Rectangle(Program.INV_SLOT_WIDTH * ++itemColumnInc, Program.INV_SLOT_HEIGHT * itemRowInc, Program.INV_SLOT_WIDTH, Program.INV_SLOT_HEIGHT) },
                { ItemID.Fancy_Box_Blue_Jewel, new System.Drawing.Rectangle(Program.INV_SLOT_WIDTH * ++itemColumnInc, Program.INV_SLOT_HEIGHT * itemRowInc, Program.INV_SLOT_WIDTH, Program.INV_SLOT_HEIGHT) },
                { ItemID.Fancy_Box_Red_Jewel, new System.Drawing.Rectangle(Program.INV_SLOT_WIDTH * ++itemColumnInc, Program.INV_SLOT_HEIGHT * itemRowInc, Program.INV_SLOT_WIDTH, Program.INV_SLOT_HEIGHT) },

                // Row 5.
                { ItemID.Hospital_ID_Card, new System.Drawing.Rectangle(Program.INV_SLOT_WIDTH * (itemColumnInc = 0), Program.INV_SLOT_HEIGHT * ++itemRowInc, Program.INV_SLOT_WIDTH, Program.INV_SLOT_HEIGHT) },
                { ItemID.Audiocassette_Tape, new System.Drawing.Rectangle(Program.INV_SLOT_WIDTH * ++itemColumnInc, Program.INV_SLOT_HEIGHT * itemRowInc, Program.INV_SLOT_WIDTH, Program.INV_SLOT_HEIGHT) },
                { ItemID.Vaccine_Sample, new System.Drawing.Rectangle(Program.INV_SLOT_WIDTH * ++itemColumnInc, Program.INV_SLOT_HEIGHT * itemRowInc, Program.INV_SLOT_WIDTH, Program.INV_SLOT_HEIGHT) },
                { ItemID.Fuse1, new System.Drawing.Rectangle(Program.INV_SLOT_WIDTH * ++itemColumnInc, Program.INV_SLOT_HEIGHT * itemRowInc, Program.INV_SLOT_WIDTH, Program.INV_SLOT_HEIGHT) },
                { ItemID.Fuse2, new System.Drawing.Rectangle(Program.INV_SLOT_WIDTH * ++itemColumnInc, Program.INV_SLOT_HEIGHT * itemRowInc, Program.INV_SLOT_WIDTH, Program.INV_SLOT_HEIGHT) },
                { ItemID.Fuse3, new System.Drawing.Rectangle(Program.INV_SLOT_WIDTH * ++itemColumnInc, Program.INV_SLOT_HEIGHT * itemRowInc, Program.INV_SLOT_WIDTH, Program.INV_SLOT_HEIGHT) },
                { ItemID.Audiocassette_Tape2, new System.Drawing.Rectangle(Program.INV_SLOT_WIDTH * ++itemColumnInc, Program.INV_SLOT_HEIGHT * itemRowInc, Program.INV_SLOT_WIDTH, Program.INV_SLOT_HEIGHT) },
                { ItemID.Tape_Player, new System.Drawing.Rectangle(Program.INV_SLOT_WIDTH * ++itemColumnInc, Program.INV_SLOT_HEIGHT * itemRowInc, Program.INV_SLOT_WIDTH, Program.INV_SLOT_HEIGHT) },
                { ItemID.Tape_Player_Tape_Inserted, new System.Drawing.Rectangle(Program.INV_SLOT_WIDTH * ++itemColumnInc, Program.INV_SLOT_HEIGHT * itemRowInc, Program.INV_SLOT_WIDTH, Program.INV_SLOT_HEIGHT) },
                { ItemID.Locker_Room_Key, new System.Drawing.Rectangle(Program.INV_SLOT_WIDTH * ++itemColumnInc, Program.INV_SLOT_HEIGHT * itemRowInc, Program.INV_SLOT_WIDTH, Program.INV_SLOT_HEIGHT) },

                // Row 6.
                { ItemID.Override_Key, new System.Drawing.Rectangle(Program.INV_SLOT_WIDTH * (itemColumnInc = 0), Program.INV_SLOT_HEIGHT * ++itemRowInc, Program.INV_SLOT_WIDTH, Program.INV_SLOT_HEIGHT) },
                { ItemID.Vaccine, new System.Drawing.Rectangle(Program.INV_SLOT_WIDTH * ++itemColumnInc, Program.INV_SLOT_HEIGHT * itemRowInc, Program.INV_SLOT_WIDTH, Program.INV_SLOT_HEIGHT) },
                { ItemID.Culture_Sample, new System.Drawing.Rectangle(Program.INV_SLOT_WIDTH * ++itemColumnInc, Program.INV_SLOT_HEIGHT * itemRowInc, Program.INV_SLOT_WIDTH, Program.INV_SLOT_HEIGHT) },
                { ItemID.Liquidfilled_Test_Tube, new System.Drawing.Rectangle(Program.INV_SLOT_WIDTH * ++itemColumnInc, Program.INV_SLOT_HEIGHT * itemRowInc, Program.INV_SLOT_WIDTH, Program.INV_SLOT_HEIGHT) },
                { ItemID.Vaccine_Base, new System.Drawing.Rectangle(Program.INV_SLOT_WIDTH * ++itemColumnInc, Program.INV_SLOT_HEIGHT * itemRowInc, Program.INV_SLOT_WIDTH, Program.INV_SLOT_HEIGHT) },

                // Row 7.
                { ItemID.Hip_Pouch, new System.Drawing.Rectangle(Program.INV_SLOT_WIDTH * (itemColumnInc = 1), Program.INV_SLOT_HEIGHT * ++itemRowInc, Program.INV_SLOT_WIDTH, Program.INV_SLOT_HEIGHT) },
                { ItemID.Iron_Defense_Coin, new System.Drawing.Rectangle(Program.INV_SLOT_WIDTH * (itemColumnInc = 5), Program.INV_SLOT_HEIGHT * itemRowInc, Program.INV_SLOT_WIDTH, Program.INV_SLOT_HEIGHT) },
                { ItemID.Assault_Coin, new System.Drawing.Rectangle(Program.INV_SLOT_WIDTH * ++itemColumnInc, Program.INV_SLOT_HEIGHT * itemRowInc, Program.INV_SLOT_WIDTH, Program.INV_SLOT_HEIGHT) },
                { ItemID.Recovery_Coin, new System.Drawing.Rectangle(Program.INV_SLOT_WIDTH * ++itemColumnInc, Program.INV_SLOT_HEIGHT * itemRowInc, Program.INV_SLOT_WIDTH, Program.INV_SLOT_HEIGHT) },
                { ItemID.Crafting_Companion, new System.Drawing.Rectangle(Program.INV_SLOT_WIDTH * ++itemColumnInc, Program.INV_SLOT_HEIGHT * itemRowInc, Program.INV_SLOT_WIDTH, Program.INV_SLOT_HEIGHT) },
                { ItemID.STARS_Field_Combat_Manual, new System.Drawing.Rectangle(Program.INV_SLOT_WIDTH * ++itemColumnInc, Program.INV_SLOT_HEIGHT * itemRowInc, Program.INV_SLOT_WIDTH, Program.INV_SLOT_HEIGHT) },

                //// Row 8.
                //{ ItemID.Gold_Star, new System.Drawing.Rectangle(Program.INV_SLOT_WIDTH * (itemColumnInc = 16), Program.INV_SLOT_HEIGHT * ++itemRowInc, Program.INV_SLOT_WIDTH, Program.INV_SLOT_HEIGHT) },
            };

            int weaponColumnInc = -1;
            int weaponRowInc = -1;
            WeaponToImageTranslation = new Dictionary<Weapon, System.Drawing.Rectangle>()
            {
                { new Weapon() { WeaponID = WeaponType.None, Attachments = WeaponParts.None }, new System.Drawing.Rectangle(Program.INV_SLOT_WIDTH * 0, Program.INV_SLOT_HEIGHT * 5, Program.INV_SLOT_WIDTH, Program.INV_SLOT_HEIGHT) },

                // Row 1.
                { new Weapon() { WeaponID = WeaponType.G19Handgun, Attachments = WeaponParts.None }, new System.Drawing.Rectangle(Program.INV_SLOT_WIDTH * (weaponColumnInc = 0), Program.INV_SLOT_HEIGHT * ++weaponRowInc, Program.INV_SLOT_WIDTH, Program.INV_SLOT_HEIGHT) },
                { new Weapon() { WeaponID = WeaponType.G19Handgun, Attachments = WeaponParts.First }, new System.Drawing.Rectangle(Program.INV_SLOT_WIDTH * ++weaponColumnInc, Program.INV_SLOT_HEIGHT * weaponRowInc, Program.INV_SLOT_WIDTH * 2, Program.INV_SLOT_HEIGHT) },
                { new Weapon() { WeaponID = WeaponType.G19Handgun, Attachments = WeaponParts.First | WeaponParts.Third }, new System.Drawing.Rectangle(Program.INV_SLOT_WIDTH * (weaponColumnInc = 3), Program.INV_SLOT_HEIGHT * weaponRowInc, Program.INV_SLOT_WIDTH * 2, Program.INV_SLOT_HEIGHT) },
                { new Weapon() { WeaponID = WeaponType.G19Handgun, Attachments = WeaponParts.First | WeaponParts.Second | WeaponParts.Third }, new System.Drawing.Rectangle(Program.INV_SLOT_WIDTH * (weaponColumnInc = 5), Program.INV_SLOT_HEIGHT * weaponRowInc, Program.INV_SLOT_WIDTH * 2, Program.INV_SLOT_HEIGHT) },
                { new Weapon() { WeaponID = WeaponType.G19Handgun, Attachments = WeaponParts.Third }, new System.Drawing.Rectangle(Program.INV_SLOT_WIDTH * (weaponColumnInc = 7), Program.INV_SLOT_HEIGHT * weaponRowInc, Program.INV_SLOT_WIDTH, Program.INV_SLOT_HEIGHT) },
                { new Weapon() { WeaponID = WeaponType.G19Handgun, Attachments = WeaponParts.Second | WeaponParts.Third }, new System.Drawing.Rectangle(Program.INV_SLOT_WIDTH * ++weaponColumnInc, Program.INV_SLOT_HEIGHT * weaponRowInc, Program.INV_SLOT_WIDTH, Program.INV_SLOT_HEIGHT) },
                { new Weapon() { WeaponID = WeaponType.G19Handgun, Attachments = WeaponParts.Second }, new System.Drawing.Rectangle(Program.INV_SLOT_WIDTH * ++weaponColumnInc, Program.INV_SLOT_HEIGHT * weaponRowInc, Program.INV_SLOT_WIDTH, Program.INV_SLOT_HEIGHT) },
                { new Weapon() { WeaponID = WeaponType.G19Handgun, Attachments = WeaponParts.First | WeaponParts.Second }, new System.Drawing.Rectangle(Program.INV_SLOT_WIDTH * (weaponColumnInc = 10), Program.INV_SLOT_HEIGHT * weaponRowInc, Program.INV_SLOT_WIDTH * 2, Program.INV_SLOT_HEIGHT) },
                { new Weapon() { WeaponID = WeaponType.SamuraiEdge, Attachments = WeaponParts.None }, new System.Drawing.Rectangle(Program.INV_SLOT_WIDTH * (weaponColumnInc = 12), Program.INV_SLOT_HEIGHT * weaponRowInc, Program.INV_SLOT_WIDTH, Program.INV_SLOT_HEIGHT) },
                { new Weapon() { WeaponID = WeaponType.G18Handgun, Attachments = WeaponParts.None }, new System.Drawing.Rectangle(Program.INV_SLOT_WIDTH * (weaponColumnInc = 16), Program.INV_SLOT_HEIGHT * weaponRowInc, Program.INV_SLOT_WIDTH, Program.INV_SLOT_HEIGHT) },
                { new Weapon() { WeaponID = WeaponType.G18BurstHandgun, Attachments = WeaponParts.None }, new System.Drawing.Rectangle(Program.INV_SLOT_WIDTH * ++weaponColumnInc, Program.INV_SLOT_HEIGHT * weaponRowInc, Program.INV_SLOT_WIDTH, Program.INV_SLOT_HEIGHT) },

                // Row 2.
                { new Weapon() { WeaponID = WeaponType.M3Shotgun, Attachments = WeaponParts.None }, new System.Drawing.Rectangle(Program.INV_SLOT_WIDTH * (weaponColumnInc = 0), Program.INV_SLOT_HEIGHT * ++weaponRowInc, Program.INV_SLOT_WIDTH, Program.INV_SLOT_HEIGHT) },
                { new Weapon() { WeaponID = WeaponType.M3Shotgun, Attachments = WeaponParts.First }, new System.Drawing.Rectangle(Program.INV_SLOT_WIDTH * ++weaponColumnInc, Program.INV_SLOT_HEIGHT * weaponRowInc, Program.INV_SLOT_WIDTH * 2, Program.INV_SLOT_HEIGHT) },
                { new Weapon() { WeaponID = WeaponType.M3Shotgun, Attachments = WeaponParts.First | WeaponParts.Third }, new System.Drawing.Rectangle(Program.INV_SLOT_WIDTH * (weaponColumnInc = 3), Program.INV_SLOT_HEIGHT * weaponRowInc, Program.INV_SLOT_WIDTH * 2, Program.INV_SLOT_HEIGHT) },
                { new Weapon() { WeaponID = WeaponType.M3Shotgun, Attachments = WeaponParts.First | WeaponParts.Second | WeaponParts.Third }, new System.Drawing.Rectangle(Program.INV_SLOT_WIDTH * (weaponColumnInc = 5), Program.INV_SLOT_HEIGHT * weaponRowInc, Program.INV_SLOT_WIDTH * 2, Program.INV_SLOT_HEIGHT) },
                { new Weapon() { WeaponID = WeaponType.M3Shotgun, Attachments = WeaponParts.Third }, new System.Drawing.Rectangle(Program.INV_SLOT_WIDTH * (weaponColumnInc = 7), Program.INV_SLOT_HEIGHT * weaponRowInc, Program.INV_SLOT_WIDTH, Program.INV_SLOT_HEIGHT) },
                { new Weapon() { WeaponID = WeaponType.M3Shotgun, Attachments = WeaponParts.Second | WeaponParts.Third }, new System.Drawing.Rectangle(Program.INV_SLOT_WIDTH * ++weaponColumnInc, Program.INV_SLOT_HEIGHT * weaponRowInc, Program.INV_SLOT_WIDTH, Program.INV_SLOT_HEIGHT) },
                { new Weapon() { WeaponID = WeaponType.M3Shotgun, Attachments = WeaponParts.Second }, new System.Drawing.Rectangle(Program.INV_SLOT_WIDTH * ++weaponColumnInc, Program.INV_SLOT_HEIGHT * weaponRowInc, Program.INV_SLOT_WIDTH, Program.INV_SLOT_HEIGHT) },
                { new Weapon() { WeaponID = WeaponType.M3Shotgun, Attachments = WeaponParts.First | WeaponParts.Second }, new System.Drawing.Rectangle(Program.INV_SLOT_WIDTH * (weaponColumnInc = 10), Program.INV_SLOT_HEIGHT * weaponRowInc, Program.INV_SLOT_WIDTH * 2, Program.INV_SLOT_HEIGHT) },
                { new Weapon() { WeaponID = WeaponType.LightningHawk, Attachments = WeaponParts.None }, new System.Drawing.Rectangle(Program.INV_SLOT_WIDTH * (weaponColumnInc = 12), Program.INV_SLOT_HEIGHT * weaponRowInc, Program.INV_SLOT_WIDTH, Program.INV_SLOT_HEIGHT) },
                { new Weapon() { WeaponID = WeaponType.LightningHawk, Attachments = WeaponParts.Second }, new System.Drawing.Rectangle(Program.INV_SLOT_WIDTH * ++weaponColumnInc, Program.INV_SLOT_HEIGHT * weaponRowInc, Program.INV_SLOT_WIDTH, Program.INV_SLOT_HEIGHT) },

                // Row 3.
                { new Weapon() { WeaponID = WeaponType.CQBRAssaultRifle, Attachments = WeaponParts.None }, new System.Drawing.Rectangle(Program.INV_SLOT_WIDTH * (weaponColumnInc = 0), Program.INV_SLOT_HEIGHT * ++weaponRowInc, Program.INV_SLOT_WIDTH * 2, Program.INV_SLOT_HEIGHT) },
                { new Weapon() { WeaponID = WeaponType.CQBRAssaultRifle, Attachments = WeaponParts.First }, new System.Drawing.Rectangle(Program.INV_SLOT_WIDTH * (weaponColumnInc = 2), Program.INV_SLOT_HEIGHT * weaponRowInc, Program.INV_SLOT_WIDTH * 2, Program.INV_SLOT_HEIGHT) },
                { new Weapon() { WeaponID = WeaponType.CQBRAssaultRifle, Attachments = WeaponParts.First | WeaponParts.Third }, new System.Drawing.Rectangle(Program.INV_SLOT_WIDTH * (weaponColumnInc = 4), Program.INV_SLOT_HEIGHT * weaponRowInc, Program.INV_SLOT_WIDTH * 2, Program.INV_SLOT_HEIGHT) },
                { new Weapon() { WeaponID = WeaponType.CQBRAssaultRifle, Attachments = WeaponParts.First | WeaponParts.Second | WeaponParts.Third }, new System.Drawing.Rectangle(Program.INV_SLOT_WIDTH * (weaponColumnInc = 6), Program.INV_SLOT_HEIGHT * weaponRowInc, Program.INV_SLOT_WIDTH * 2, Program.INV_SLOT_HEIGHT) },
                { new Weapon() { WeaponID = WeaponType.CQBRAssaultRifle, Attachments = WeaponParts.Third }, new System.Drawing.Rectangle(Program.INV_SLOT_WIDTH * (weaponColumnInc = 8), Program.INV_SLOT_HEIGHT * weaponRowInc, Program.INV_SLOT_WIDTH * 2, Program.INV_SLOT_HEIGHT) },
                { new Weapon() { WeaponID = WeaponType.CQBRAssaultRifle, Attachments = WeaponParts.Second | WeaponParts.Third }, new System.Drawing.Rectangle(Program.INV_SLOT_WIDTH * (weaponColumnInc = 10), Program.INV_SLOT_HEIGHT * weaponRowInc, Program.INV_SLOT_WIDTH * 2, Program.INV_SLOT_HEIGHT) },
                { new Weapon() { WeaponID = WeaponType.CQBRAssaultRifle, Attachments = WeaponParts.Second }, new System.Drawing.Rectangle(Program.INV_SLOT_WIDTH * (weaponColumnInc = 12), Program.INV_SLOT_HEIGHT * weaponRowInc, Program.INV_SLOT_WIDTH * 2, Program.INV_SLOT_HEIGHT) },
                { new Weapon() { WeaponID = WeaponType.CQBRAssaultRifle, Attachments = WeaponParts.First | WeaponParts.Second }, new System.Drawing.Rectangle(Program.INV_SLOT_WIDTH * (weaponColumnInc = 14), Program.INV_SLOT_HEIGHT * weaponRowInc, Program.INV_SLOT_WIDTH * 2, Program.INV_SLOT_HEIGHT) },


                // Row 4.
                { new Weapon() { WeaponID = WeaponType.RocketLauncher, Attachments = WeaponParts.None }, new System.Drawing.Rectangle(Program.INV_SLOT_WIDTH * (weaponColumnInc = 6), Program.INV_SLOT_HEIGHT * ++weaponRowInc, Program.INV_SLOT_WIDTH * 2, Program.INV_SLOT_HEIGHT) },
                { new Weapon() { WeaponID = WeaponType.InfiniteCQBRAssaultRifle, Attachments = WeaponParts.None }, new System.Drawing.Rectangle(Program.INV_SLOT_WIDTH * (weaponColumnInc = 8), Program.INV_SLOT_HEIGHT * weaponRowInc, Program.INV_SLOT_WIDTH * 2, Program.INV_SLOT_HEIGHT) },

                // Row 5.
                { new Weapon() { WeaponID = WeaponType.CombatKnife, Attachments = WeaponParts.None }, new System.Drawing.Rectangle(Program.INV_SLOT_WIDTH * (weaponColumnInc = 0), Program.INV_SLOT_HEIGHT * ++weaponRowInc, Program.INV_SLOT_WIDTH, Program.INV_SLOT_HEIGHT) },
                { new Weapon() { WeaponID = WeaponType.SurvivalKnife, Attachments = WeaponParts.None }, new System.Drawing.Rectangle(Program.INV_SLOT_WIDTH * ++weaponColumnInc, Program.INV_SLOT_HEIGHT * weaponRowInc, Program.INV_SLOT_WIDTH, Program.INV_SLOT_HEIGHT) },
                { new Weapon() { WeaponID = WeaponType.InfiniteMUPHandgun, Attachments = WeaponParts.None }, new System.Drawing.Rectangle(Program.INV_SLOT_WIDTH * (weaponColumnInc = 4), Program.INV_SLOT_HEIGHT * weaponRowInc, Program.INV_SLOT_WIDTH, Program.INV_SLOT_HEIGHT) },
                { new Weapon() { WeaponID = WeaponType.RAIDEN, Attachments = WeaponParts.None }, new System.Drawing.Rectangle(Program.INV_SLOT_WIDTH * ++weaponColumnInc, Program.INV_SLOT_HEIGHT * weaponRowInc, Program.INV_SLOT_WIDTH * 2, Program.INV_SLOT_HEIGHT) },
                { new Weapon() { WeaponID = WeaponType.HotDogger, Attachments = WeaponParts.None }, new System.Drawing.Rectangle(Program.INV_SLOT_WIDTH * (weaponColumnInc = 7), Program.INV_SLOT_HEIGHT * weaponRowInc, Program.INV_SLOT_WIDTH, Program.INV_SLOT_HEIGHT) },
                { new Weapon() { WeaponID = WeaponType.HandGrenade, Attachments = WeaponParts.None }, new System.Drawing.Rectangle(Program.INV_SLOT_WIDTH * (weaponColumnInc = 9), Program.INV_SLOT_HEIGHT * weaponRowInc, Program.INV_SLOT_WIDTH, Program.INV_SLOT_HEIGHT) },
                { new Weapon() { WeaponID = WeaponType.FlashGrenade, Attachments = WeaponParts.None }, new System.Drawing.Rectangle(Program.INV_SLOT_WIDTH * ++weaponColumnInc, Program.INV_SLOT_HEIGHT * weaponRowInc, Program.INV_SLOT_WIDTH, Program.INV_SLOT_HEIGHT) },
                { new Weapon() { WeaponID = WeaponType.MGLGrenadeLauncher, Attachments = WeaponParts.None }, new System.Drawing.Rectangle(Program.INV_SLOT_WIDTH * ++weaponColumnInc, Program.INV_SLOT_HEIGHT * weaponRowInc, Program.INV_SLOT_WIDTH * 2, Program.INV_SLOT_HEIGHT) },

            };
        }

        public static void GenerateBrushes(System.Drawing.Image item, System.Drawing.Image weapon, System.Drawing.Image error)
        {
            ItemToImageBrush = new Dictionary<ItemID, System.Drawing.TextureBrush>(ItemToImageTranslation.Select(kvp => new KeyValuePair<ItemID, System.Drawing.TextureBrush>(kvp.Key, new System.Drawing.TextureBrush(item, kvp.Value))));
            WeaponToImageBrush = new Dictionary<Weapon, System.Drawing.TextureBrush>(WeaponToImageTranslation.Select(kvp => new KeyValuePair<Weapon, System.Drawing.TextureBrush>(kvp.Key, new System.Drawing.TextureBrush(weapon, kvp.Value))));
            ErrorToImageBrush = new System.Drawing.TextureBrush(error, new System.Drawing.Rectangle(0, 0, INV_SLOT_WIDTH, INV_SLOT_HEIGHT));
        }
    }
}
