using SRTPluginBase;
using SRTPluginProviderRE3.Structures;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace SRTPluginUIRE3WinForms
{
    public class SRTPluginUIRE3WinForms : IPluginUI
    {
        internal static PluginInfo _Info = new PluginInfo();
        public IPluginInfo Info => _Info;

        private IPluginHostDelegates hostDelegates;
        private ApplicationContext applicationContext;
        private Task applicationTask;
        public static ContextMenuStrip contextMenuStrip;

        [STAThread]
        public int Startup(IPluginHostDelegates hostDelegates)
        {
            this.hostDelegates = hostDelegates;

            // Must be before any rendering happens, including creation of ContextMenuStrip in Program class.
            Application.SetHighDpiMode(HighDpiMode.SystemAware);
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

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

        public int Shutdown()
        {
            // Clean up the context.
            if (applicationContext != null)
            {
                // Clean up the form.
                if (applicationContext.MainForm != null)
                {
                    applicationContext.MainForm.Close();
                    applicationContext.MainForm.Dispose();
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
        public static Options programSpecialOptions;

        public static readonly string srtVersion = string.Format("v{0}.{1}.{2}.{3}", SRTPluginUIRE3WinForms._Info.VersionMajor, SRTPluginUIRE3WinForms._Info.VersionMinor, SRTPluginUIRE3WinForms._Info.VersionBuild, SRTPluginUIRE3WinForms._Info.VersionRevision);
        public static readonly string srtTitle = string.Format("RE3(2020) SRT - {0}", srtVersion);

        public static int INV_SLOT_WIDTH;
        public static int INV_SLOT_HEIGHT;

        public static IReadOnlyDictionary<ItemEnumeration, System.Drawing.Rectangle> ItemToImageTranslation;
        public static IReadOnlyDictionary<Weapon, System.Drawing.Rectangle> WeaponToImageTranslation;

        public static void Main(string[] args) // NOT THE REAL ENTRYPOINT. THIS IS A PLACEHOLDER FOR LEGACY CODE TO BE PORTED.
        {
            try
            {
                // Handle command-line parameters.
                programSpecialOptions = new Options();
                programSpecialOptions.GetOptions();

                foreach (string arg in args)
                {
                    if (arg.Equals("--Help", StringComparison.InvariantCultureIgnoreCase))
                    {
                        StringBuilder message = new StringBuilder("Command-line arguments:\r\n\r\n");
                        message.AppendFormat("{0}\r\n\t{1}\r\n\r\n", "--No-Titlebar", "Hide the titlebar and window frame.");
                        message.AppendFormat("{0}\r\n\t{1}\r\n\r\n", "--Always-On-Top", "Always appear on top of other windows.");
                        message.AppendFormat("{0}\r\n\t{1}\r\n\r\n", "--Transparent", "Make the background transparent.");
                        message.AppendFormat("{0}\r\n\t{1}\r\n\r\n", "--ScalingFactor=n", "Set the inventory slot scaling factor on a scale of 0.0 to 1.0. Default: 0.75 (75%)");
                        message.AppendFormat("{0}\r\n\t{1}\r\n\r\n", "--NoInventory", "Disables the inventory display.");
                        message.AppendFormat("{0}\r\n\t{1}\r\n\r\n", "--Debug", "Debug mode.");

                        MessageBox.Show(null, message.ToString().Trim(), string.Empty, MessageBoxButtons.OK, MessageBoxIcon.Information);
                        Environment.Exit(0);
                    }

                    if (arg.Equals("--No-Titlebar", StringComparison.InvariantCultureIgnoreCase))
                        programSpecialOptions.Flags |= ProgramFlags.NoTitleBar;

                    if (arg.Equals("--Always-On-Top", StringComparison.InvariantCultureIgnoreCase))
                        programSpecialOptions.Flags |= ProgramFlags.AlwaysOnTop;

                    if (arg.Equals("--Transparent", StringComparison.InvariantCultureIgnoreCase))
                        programSpecialOptions.Flags |= ProgramFlags.Transparent;

                    if (arg.Equals("--NoInventory", StringComparison.InvariantCultureIgnoreCase))
                        programSpecialOptions.Flags |= ProgramFlags.NoInventory;

                    if (arg.StartsWith("--ScalingFactor=", StringComparison.InvariantCultureIgnoreCase))
                        if (!double.TryParse(arg.Split(new char[1] { '=' }, 2, StringSplitOptions.None)[1], out programSpecialOptions.ScalingFactor))
                            programSpecialOptions.ScalingFactor = 0.75d; // Default scaling factor for the inventory images. If we fail to process the user input, ensure this gets set to the default value just in case.

                    if (arg.Equals("--Debug", StringComparison.InvariantCultureIgnoreCase))
                        programSpecialOptions.Flags |= ProgramFlags.Debug;
                }

                // Set item slot sizes after scaling is determined.
                INV_SLOT_WIDTH = (int)Math.Round(112d * programSpecialOptions.ScalingFactor, MidpointRounding.AwayFromZero); // Individual inventory slot width.
                INV_SLOT_HEIGHT = (int)Math.Round(112d * programSpecialOptions.ScalingFactor, MidpointRounding.AwayFromZero); // Individual inventory slot height.

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
            ItemToImageTranslation = new Dictionary<ItemEnumeration, System.Drawing.Rectangle>()
            {
                { ItemEnumeration.None, new System.Drawing.Rectangle(0, 0, 0, 0) },

                // Row 0.
                { ItemEnumeration.First_Aid_Spray, new System.Drawing.Rectangle(Program.INV_SLOT_WIDTH * (itemColumnInc = 0), Program.INV_SLOT_HEIGHT * ++itemRowInc, Program.INV_SLOT_WIDTH, Program.INV_SLOT_HEIGHT) },
                { ItemEnumeration.Green_Herb, new System.Drawing.Rectangle(Program.INV_SLOT_WIDTH * ++itemColumnInc, Program.INV_SLOT_HEIGHT * itemRowInc, Program.INV_SLOT_WIDTH, Program.INV_SLOT_HEIGHT) },
                { ItemEnumeration.Red_Herb, new System.Drawing.Rectangle(Program.INV_SLOT_WIDTH * ++itemColumnInc, Program.INV_SLOT_HEIGHT * itemRowInc, Program.INV_SLOT_WIDTH, Program.INV_SLOT_HEIGHT) },
                { ItemEnumeration.Mixed_Herb_GG, new System.Drawing.Rectangle(Program.INV_SLOT_WIDTH * ++itemColumnInc, Program.INV_SLOT_HEIGHT * itemRowInc, Program.INV_SLOT_WIDTH, Program.INV_SLOT_HEIGHT) },
                { ItemEnumeration.Mixed_Herb_GR, new System.Drawing.Rectangle(Program.INV_SLOT_WIDTH * ++itemColumnInc, Program.INV_SLOT_HEIGHT * itemRowInc, Program.INV_SLOT_WIDTH, Program.INV_SLOT_HEIGHT) },
                { ItemEnumeration.Mixed_Herb_GGG, new System.Drawing.Rectangle(Program.INV_SLOT_WIDTH * ++itemColumnInc, Program.INV_SLOT_HEIGHT * itemRowInc, Program.INV_SLOT_WIDTH, Program.INV_SLOT_HEIGHT) },
                { ItemEnumeration.Green_Herb2, new System.Drawing.Rectangle(Program.INV_SLOT_WIDTH * ++itemColumnInc, Program.INV_SLOT_HEIGHT * itemRowInc, Program.INV_SLOT_WIDTH, Program.INV_SLOT_HEIGHT) },
                { ItemEnumeration.Red_Herb2, new System.Drawing.Rectangle(Program.INV_SLOT_WIDTH * ++itemColumnInc, Program.INV_SLOT_HEIGHT * itemRowInc, Program.INV_SLOT_WIDTH, Program.INV_SLOT_HEIGHT) },

                // Row 1.
                { ItemEnumeration.Handgun_Ammo, new System.Drawing.Rectangle(Program.INV_SLOT_WIDTH * (itemColumnInc = 0), Program.INV_SLOT_HEIGHT * ++itemRowInc, Program.INV_SLOT_WIDTH, Program.INV_SLOT_HEIGHT) },
                { ItemEnumeration.Shotgun_Shells, new System.Drawing.Rectangle(Program.INV_SLOT_WIDTH * ++itemColumnInc, Program.INV_SLOT_HEIGHT * itemRowInc, Program.INV_SLOT_WIDTH, Program.INV_SLOT_HEIGHT) },
                { ItemEnumeration.Assault_Rifle_Ammo, new System.Drawing.Rectangle(Program.INV_SLOT_WIDTH * ++itemColumnInc, Program.INV_SLOT_HEIGHT * itemRowInc, Program.INV_SLOT_WIDTH, Program.INV_SLOT_HEIGHT) },
                { ItemEnumeration.MAG_Ammo, new System.Drawing.Rectangle(Program.INV_SLOT_WIDTH * ++itemColumnInc, Program.INV_SLOT_HEIGHT * itemRowInc, Program.INV_SLOT_WIDTH, Program.INV_SLOT_HEIGHT) },
                { ItemEnumeration.Acid_Rounds, new System.Drawing.Rectangle(Program.INV_SLOT_WIDTH * ++itemColumnInc, Program.INV_SLOT_HEIGHT * itemRowInc, Program.INV_SLOT_WIDTH, Program.INV_SLOT_HEIGHT) },
                { ItemEnumeration.Flame_Rounds, new System.Drawing.Rectangle(Program.INV_SLOT_WIDTH * ++itemColumnInc, Program.INV_SLOT_HEIGHT * itemRowInc, Program.INV_SLOT_WIDTH, Program.INV_SLOT_HEIGHT) },
                { ItemEnumeration.Explosive_Rounds, new System.Drawing.Rectangle(Program.INV_SLOT_WIDTH * ++itemColumnInc, Program.INV_SLOT_HEIGHT * itemRowInc, Program.INV_SLOT_WIDTH, Program.INV_SLOT_HEIGHT) },
                { ItemEnumeration.Mine_Rounds, new System.Drawing.Rectangle(Program.INV_SLOT_WIDTH * ++itemColumnInc, Program.INV_SLOT_HEIGHT * itemRowInc, Program.INV_SLOT_WIDTH, Program.INV_SLOT_HEIGHT) },
                { ItemEnumeration.Gunpowder, new System.Drawing.Rectangle(Program.INV_SLOT_WIDTH * ++itemColumnInc, Program.INV_SLOT_HEIGHT * itemRowInc, Program.INV_SLOT_WIDTH, Program.INV_SLOT_HEIGHT) },
                { ItemEnumeration.HighGrade_Gunpowder, new System.Drawing.Rectangle(Program.INV_SLOT_WIDTH * ++itemColumnInc, Program.INV_SLOT_HEIGHT * itemRowInc, Program.INV_SLOT_WIDTH, Program.INV_SLOT_HEIGHT) },
                { ItemEnumeration.Explosive_A, new System.Drawing.Rectangle(Program.INV_SLOT_WIDTH * ++itemColumnInc, Program.INV_SLOT_HEIGHT * itemRowInc, Program.INV_SLOT_WIDTH, Program.INV_SLOT_HEIGHT) },
                { ItemEnumeration.Explosive_B, new System.Drawing.Rectangle(Program.INV_SLOT_WIDTH * ++itemColumnInc, Program.INV_SLOT_HEIGHT * itemRowInc, Program.INV_SLOT_WIDTH, Program.INV_SLOT_HEIGHT) },

                // Row 2.
                { ItemEnumeration.Moderator_Handgun, new System.Drawing.Rectangle(Program.INV_SLOT_WIDTH * (itemColumnInc = 0), Program.INV_SLOT_HEIGHT * ++itemRowInc, Program.INV_SLOT_WIDTH, Program.INV_SLOT_HEIGHT) },
                { ItemEnumeration.Dot_Sight_Handgun, new System.Drawing.Rectangle(Program.INV_SLOT_WIDTH * ++itemColumnInc, Program.INV_SLOT_HEIGHT * itemRowInc, Program.INV_SLOT_WIDTH, Program.INV_SLOT_HEIGHT) },
                { ItemEnumeration.Extended_Magazine_Handgun, new System.Drawing.Rectangle(Program.INV_SLOT_WIDTH * ++itemColumnInc, Program.INV_SLOT_HEIGHT * itemRowInc, Program.INV_SLOT_WIDTH, Program.INV_SLOT_HEIGHT) },
                { ItemEnumeration.SemiAuto_Barrel_Shotgun, new System.Drawing.Rectangle(Program.INV_SLOT_WIDTH * ++itemColumnInc, Program.INV_SLOT_HEIGHT * itemRowInc, Program.INV_SLOT_WIDTH, Program.INV_SLOT_HEIGHT) },
                { ItemEnumeration.Tactical_Stock_Shotgun, new System.Drawing.Rectangle(Program.INV_SLOT_WIDTH * ++itemColumnInc, Program.INV_SLOT_HEIGHT * itemRowInc, Program.INV_SLOT_WIDTH, Program.INV_SLOT_HEIGHT) },
                { ItemEnumeration.Shell_Holder_Shotgun, new System.Drawing.Rectangle(Program.INV_SLOT_WIDTH * ++itemColumnInc, Program.INV_SLOT_HEIGHT * itemRowInc, Program.INV_SLOT_WIDTH, Program.INV_SLOT_HEIGHT) },
                { ItemEnumeration.Scope_Assault_Rifle, new System.Drawing.Rectangle(Program.INV_SLOT_WIDTH * ++itemColumnInc, Program.INV_SLOT_HEIGHT * itemRowInc, Program.INV_SLOT_WIDTH, Program.INV_SLOT_HEIGHT) },
                { ItemEnumeration.Dual_Magazine_Assault_Rifle, new System.Drawing.Rectangle(Program.INV_SLOT_WIDTH * ++itemColumnInc, Program.INV_SLOT_HEIGHT * itemRowInc, Program.INV_SLOT_WIDTH, Program.INV_SLOT_HEIGHT) },
                { ItemEnumeration.Tactical_Grip_Assault_Rifle, new System.Drawing.Rectangle(Program.INV_SLOT_WIDTH * ++itemColumnInc, Program.INV_SLOT_HEIGHT * itemRowInc, Program.INV_SLOT_WIDTH, Program.INV_SLOT_HEIGHT) },
                { ItemEnumeration.Extended_Barrel_MAG, new System.Drawing.Rectangle(Program.INV_SLOT_WIDTH * ++itemColumnInc, Program.INV_SLOT_HEIGHT * itemRowInc, Program.INV_SLOT_WIDTH, Program.INV_SLOT_HEIGHT) },
                { ItemEnumeration.Supply_Crate_Acid_Rounds, new System.Drawing.Rectangle(Program.INV_SLOT_WIDTH * ++itemColumnInc, Program.INV_SLOT_HEIGHT * itemRowInc, Program.INV_SLOT_WIDTH, Program.INV_SLOT_HEIGHT) },
                { ItemEnumeration.Supply_Crate_Extended_Barrel_MAG, new System.Drawing.Rectangle(Program.INV_SLOT_WIDTH * ++itemColumnInc, Program.INV_SLOT_HEIGHT * itemRowInc, Program.INV_SLOT_WIDTH, Program.INV_SLOT_HEIGHT) },
                { ItemEnumeration.Supply_Crate_Extended_Magazine_Handgun, new System.Drawing.Rectangle(Program.INV_SLOT_WIDTH * ++itemColumnInc, Program.INV_SLOT_HEIGHT * itemRowInc, Program.INV_SLOT_WIDTH, Program.INV_SLOT_HEIGHT) },
                { ItemEnumeration.Supply_Crate_Flame_Rounds, new System.Drawing.Rectangle(Program.INV_SLOT_WIDTH * ++itemColumnInc, Program.INV_SLOT_HEIGHT * itemRowInc, Program.INV_SLOT_WIDTH, Program.INV_SLOT_HEIGHT) },
                { ItemEnumeration.Supply_Crate_Moderator_Handgun, new System.Drawing.Rectangle(Program.INV_SLOT_WIDTH * ++itemColumnInc, Program.INV_SLOT_HEIGHT * itemRowInc, Program.INV_SLOT_WIDTH, Program.INV_SLOT_HEIGHT) },
                { ItemEnumeration.Supply_Crate_Shotgun_Shells, new System.Drawing.Rectangle(Program.INV_SLOT_WIDTH * ++itemColumnInc, Program.INV_SLOT_HEIGHT * itemRowInc, Program.INV_SLOT_WIDTH, Program.INV_SLOT_HEIGHT) },

                //Row 3.
                { ItemEnumeration.Battery, new System.Drawing.Rectangle(Program.INV_SLOT_WIDTH * (itemColumnInc = 0), Program.INV_SLOT_HEIGHT * ++itemRowInc, Program.INV_SLOT_WIDTH, Program.INV_SLOT_HEIGHT) },
                { ItemEnumeration.Safety_Deposit_Key, new System.Drawing.Rectangle(Program.INV_SLOT_WIDTH * ++itemColumnInc, Program.INV_SLOT_HEIGHT * itemRowInc, Program.INV_SLOT_WIDTH, Program.INV_SLOT_HEIGHT) },
                { ItemEnumeration.Detonator_No_Battery, new System.Drawing.Rectangle(Program.INV_SLOT_WIDTH * ++itemColumnInc, Program.INV_SLOT_HEIGHT * itemRowInc, Program.INV_SLOT_WIDTH, Program.INV_SLOT_HEIGHT) },
                { ItemEnumeration.Brads_ID_Card, new System.Drawing.Rectangle(Program.INV_SLOT_WIDTH * ++itemColumnInc, Program.INV_SLOT_HEIGHT * itemRowInc, Program.INV_SLOT_WIDTH, Program.INV_SLOT_HEIGHT) },
                { ItemEnumeration.Detonator, new System.Drawing.Rectangle(Program.INV_SLOT_WIDTH * ++itemColumnInc, Program.INV_SLOT_HEIGHT * itemRowInc, Program.INV_SLOT_WIDTH, Program.INV_SLOT_HEIGHT) },
                { ItemEnumeration.Detonator2, new System.Drawing.Rectangle(Program.INV_SLOT_WIDTH * itemColumnInc, Program.INV_SLOT_HEIGHT * itemRowInc, Program.INV_SLOT_WIDTH, Program.INV_SLOT_HEIGHT) },
                { ItemEnumeration.Lock_Pick, new System.Drawing.Rectangle(Program.INV_SLOT_WIDTH * (itemColumnInc = 8), Program.INV_SLOT_HEIGHT * itemRowInc, Program.INV_SLOT_WIDTH, Program.INV_SLOT_HEIGHT) },
                { ItemEnumeration.Bolt_Cutters, new System.Drawing.Rectangle(Program.INV_SLOT_WIDTH * ++itemColumnInc, Program.INV_SLOT_HEIGHT * itemRowInc, Program.INV_SLOT_WIDTH, Program.INV_SLOT_HEIGHT) },

                // Row 4.
                { ItemEnumeration.Fire_Hose, new System.Drawing.Rectangle(Program.INV_SLOT_WIDTH * (itemColumnInc = 0), Program.INV_SLOT_HEIGHT * ++itemRowInc, Program.INV_SLOT_WIDTH, Program.INV_SLOT_HEIGHT) },
                { ItemEnumeration.Fire_Hose2, new System.Drawing.Rectangle(Program.INV_SLOT_WIDTH * itemColumnInc, Program.INV_SLOT_HEIGHT * itemRowInc, Program.INV_SLOT_WIDTH, Program.INV_SLOT_HEIGHT) },
                { ItemEnumeration.Kendos_Gate_Key, new System.Drawing.Rectangle(Program.INV_SLOT_WIDTH * ++itemColumnInc, Program.INV_SLOT_HEIGHT * itemRowInc, Program.INV_SLOT_WIDTH, Program.INV_SLOT_HEIGHT) },
                { ItemEnumeration.Battery_Pack, new System.Drawing.Rectangle(Program.INV_SLOT_WIDTH * ++itemColumnInc, Program.INV_SLOT_HEIGHT * itemRowInc, Program.INV_SLOT_WIDTH * 2, Program.INV_SLOT_HEIGHT) },
                { ItemEnumeration.Case_Lock_Pick, new System.Drawing.Rectangle(Program.INV_SLOT_WIDTH * (itemColumnInc = 4), Program.INV_SLOT_HEIGHT * itemRowInc, Program.INV_SLOT_WIDTH, Program.INV_SLOT_HEIGHT) },
                { ItemEnumeration.Green_Jewel, new System.Drawing.Rectangle(Program.INV_SLOT_WIDTH * ++itemColumnInc, Program.INV_SLOT_HEIGHT * itemRowInc, Program.INV_SLOT_WIDTH, Program.INV_SLOT_HEIGHT) },
                { ItemEnumeration.Blue_Jewel, new System.Drawing.Rectangle(Program.INV_SLOT_WIDTH * ++itemColumnInc, Program.INV_SLOT_HEIGHT * itemRowInc, Program.INV_SLOT_WIDTH, Program.INV_SLOT_HEIGHT) },
                { ItemEnumeration.Red_Jewel, new System.Drawing.Rectangle(Program.INV_SLOT_WIDTH * ++itemColumnInc, Program.INV_SLOT_HEIGHT * itemRowInc, Program.INV_SLOT_WIDTH, Program.INV_SLOT_HEIGHT) },
                { ItemEnumeration.Fancy_Box_Green_Jewel, new System.Drawing.Rectangle(Program.INV_SLOT_WIDTH * ++itemColumnInc, Program.INV_SLOT_HEIGHT * itemRowInc, Program.INV_SLOT_WIDTH, Program.INV_SLOT_HEIGHT) },
                { ItemEnumeration.Fancy_Box_Blue_Jewel, new System.Drawing.Rectangle(Program.INV_SLOT_WIDTH * ++itemColumnInc, Program.INV_SLOT_HEIGHT * itemRowInc, Program.INV_SLOT_WIDTH, Program.INV_SLOT_HEIGHT) },
                { ItemEnumeration.Fancy_Box_Red_Jewel, new System.Drawing.Rectangle(Program.INV_SLOT_WIDTH * ++itemColumnInc, Program.INV_SLOT_HEIGHT * itemRowInc, Program.INV_SLOT_WIDTH, Program.INV_SLOT_HEIGHT) },

                // Row 5.
                { ItemEnumeration.Hospital_ID_Card, new System.Drawing.Rectangle(Program.INV_SLOT_WIDTH * (itemColumnInc = 0), Program.INV_SLOT_HEIGHT * ++itemRowInc, Program.INV_SLOT_WIDTH, Program.INV_SLOT_HEIGHT) },
                { ItemEnumeration.Audiocassette_Tape, new System.Drawing.Rectangle(Program.INV_SLOT_WIDTH * ++itemColumnInc, Program.INV_SLOT_HEIGHT * itemRowInc, Program.INV_SLOT_WIDTH, Program.INV_SLOT_HEIGHT) },
                { ItemEnumeration.Vaccine_Sample, new System.Drawing.Rectangle(Program.INV_SLOT_WIDTH * ++itemColumnInc, Program.INV_SLOT_HEIGHT * itemRowInc, Program.INV_SLOT_WIDTH, Program.INV_SLOT_HEIGHT) },
                { ItemEnumeration.Fuse1, new System.Drawing.Rectangle(Program.INV_SLOT_WIDTH * ++itemColumnInc, Program.INV_SLOT_HEIGHT * itemRowInc, Program.INV_SLOT_WIDTH, Program.INV_SLOT_HEIGHT) },
                { ItemEnumeration.Fuse2, new System.Drawing.Rectangle(Program.INV_SLOT_WIDTH * ++itemColumnInc, Program.INV_SLOT_HEIGHT * itemRowInc, Program.INV_SLOT_WIDTH, Program.INV_SLOT_HEIGHT) },
                { ItemEnumeration.Fuse3, new System.Drawing.Rectangle(Program.INV_SLOT_WIDTH * ++itemColumnInc, Program.INV_SLOT_HEIGHT * itemRowInc, Program.INV_SLOT_WIDTH, Program.INV_SLOT_HEIGHT) },
                { ItemEnumeration.Audiocassette_Tape2, new System.Drawing.Rectangle(Program.INV_SLOT_WIDTH * ++itemColumnInc, Program.INV_SLOT_HEIGHT * itemRowInc, Program.INV_SLOT_WIDTH, Program.INV_SLOT_HEIGHT) },
                { ItemEnumeration.Tape_Player, new System.Drawing.Rectangle(Program.INV_SLOT_WIDTH * ++itemColumnInc, Program.INV_SLOT_HEIGHT * itemRowInc, Program.INV_SLOT_WIDTH, Program.INV_SLOT_HEIGHT) },
                { ItemEnumeration.Tape_Player_Tape_Inserted, new System.Drawing.Rectangle(Program.INV_SLOT_WIDTH * ++itemColumnInc, Program.INV_SLOT_HEIGHT * itemRowInc, Program.INV_SLOT_WIDTH, Program.INV_SLOT_HEIGHT) },
                { ItemEnumeration.Locker_Room_Key, new System.Drawing.Rectangle(Program.INV_SLOT_WIDTH * ++itemColumnInc, Program.INV_SLOT_HEIGHT * itemRowInc, Program.INV_SLOT_WIDTH, Program.INV_SLOT_HEIGHT) },

                // Row 6.
                { ItemEnumeration.Override_Key, new System.Drawing.Rectangle(Program.INV_SLOT_WIDTH * (itemColumnInc = 0), Program.INV_SLOT_HEIGHT * ++itemRowInc, Program.INV_SLOT_WIDTH, Program.INV_SLOT_HEIGHT) },
                { ItemEnumeration.Vaccine, new System.Drawing.Rectangle(Program.INV_SLOT_WIDTH * ++itemColumnInc, Program.INV_SLOT_HEIGHT * itemRowInc, Program.INV_SLOT_WIDTH, Program.INV_SLOT_HEIGHT) },
                { ItemEnumeration.Culture_Sample, new System.Drawing.Rectangle(Program.INV_SLOT_WIDTH * ++itemColumnInc, Program.INV_SLOT_HEIGHT * itemRowInc, Program.INV_SLOT_WIDTH, Program.INV_SLOT_HEIGHT) },
                { ItemEnumeration.Liquidfilled_Test_Tube, new System.Drawing.Rectangle(Program.INV_SLOT_WIDTH * ++itemColumnInc, Program.INV_SLOT_HEIGHT * itemRowInc, Program.INV_SLOT_WIDTH, Program.INV_SLOT_HEIGHT) },
                { ItemEnumeration.Vaccine_Base, new System.Drawing.Rectangle(Program.INV_SLOT_WIDTH * ++itemColumnInc, Program.INV_SLOT_HEIGHT * itemRowInc, Program.INV_SLOT_WIDTH, Program.INV_SLOT_HEIGHT) },

                // Row 7.
                { ItemEnumeration.Hip_Pouch, new System.Drawing.Rectangle(Program.INV_SLOT_WIDTH * (itemColumnInc = 1), Program.INV_SLOT_HEIGHT * ++itemRowInc, Program.INV_SLOT_WIDTH, Program.INV_SLOT_HEIGHT) },
                { ItemEnumeration.Iron_Defense_Coin, new System.Drawing.Rectangle(Program.INV_SLOT_WIDTH * (itemColumnInc = 5), Program.INV_SLOT_HEIGHT * itemRowInc, Program.INV_SLOT_WIDTH, Program.INV_SLOT_HEIGHT) },
                { ItemEnumeration.Assault_Coin, new System.Drawing.Rectangle(Program.INV_SLOT_WIDTH * ++itemColumnInc, Program.INV_SLOT_HEIGHT * itemRowInc, Program.INV_SLOT_WIDTH, Program.INV_SLOT_HEIGHT) },
                { ItemEnumeration.Recovery_Coin, new System.Drawing.Rectangle(Program.INV_SLOT_WIDTH * ++itemColumnInc, Program.INV_SLOT_HEIGHT * itemRowInc, Program.INV_SLOT_WIDTH, Program.INV_SLOT_HEIGHT) },
                { ItemEnumeration.Crafting_Companion, new System.Drawing.Rectangle(Program.INV_SLOT_WIDTH * ++itemColumnInc, Program.INV_SLOT_HEIGHT * itemRowInc, Program.INV_SLOT_WIDTH, Program.INV_SLOT_HEIGHT) },
                { ItemEnumeration.STARS_Field_Combat_Manual, new System.Drawing.Rectangle(Program.INV_SLOT_WIDTH * ++itemColumnInc, Program.INV_SLOT_HEIGHT * itemRowInc, Program.INV_SLOT_WIDTH, Program.INV_SLOT_HEIGHT) },

                //// Row 8.
                //{ ItemEnumeration.Gold_Star, new System.Drawing.Rectangle(Program.INV_SLOT_WIDTH * (itemColumnInc = 16), Program.INV_SLOT_HEIGHT * ++itemRowInc, Program.INV_SLOT_WIDTH, Program.INV_SLOT_HEIGHT) },
            };

            int weaponColumnInc = -1;
            int weaponRowInc = -1;
            WeaponToImageTranslation = new Dictionary<Weapon, System.Drawing.Rectangle>()
            {
                { new Weapon() { WeaponID = WeaponEnumeration.None, Attachments = AttachmentsFlag.None }, new System.Drawing.Rectangle(0, 0, 0, 0) },

                // Row 1.
                { new Weapon() { WeaponID = WeaponEnumeration.G19_Handgun, Attachments = AttachmentsFlag.None }, new System.Drawing.Rectangle(Program.INV_SLOT_WIDTH * (weaponColumnInc = 0), Program.INV_SLOT_HEIGHT * ++weaponRowInc, Program.INV_SLOT_WIDTH, Program.INV_SLOT_HEIGHT) },
                { new Weapon() { WeaponID = WeaponEnumeration.G19_Handgun, Attachments = AttachmentsFlag.First }, new System.Drawing.Rectangle(Program.INV_SLOT_WIDTH * ++weaponColumnInc, Program.INV_SLOT_HEIGHT * weaponRowInc, Program.INV_SLOT_WIDTH * 2, Program.INV_SLOT_HEIGHT) },
                { new Weapon() { WeaponID = WeaponEnumeration.G19_Handgun, Attachments = AttachmentsFlag.First | AttachmentsFlag.Third }, new System.Drawing.Rectangle(Program.INV_SLOT_WIDTH * (weaponColumnInc = 3), Program.INV_SLOT_HEIGHT * weaponRowInc, Program.INV_SLOT_WIDTH * 2, Program.INV_SLOT_HEIGHT) },
                { new Weapon() { WeaponID = WeaponEnumeration.G19_Handgun, Attachments = AttachmentsFlag.First | AttachmentsFlag.Second | AttachmentsFlag.Third }, new System.Drawing.Rectangle(Program.INV_SLOT_WIDTH * (weaponColumnInc = 5), Program.INV_SLOT_HEIGHT * weaponRowInc, Program.INV_SLOT_WIDTH * 2, Program.INV_SLOT_HEIGHT) },
                { new Weapon() { WeaponID = WeaponEnumeration.G19_Handgun, Attachments = AttachmentsFlag.Third }, new System.Drawing.Rectangle(Program.INV_SLOT_WIDTH * (weaponColumnInc = 7), Program.INV_SLOT_HEIGHT * weaponRowInc, Program.INV_SLOT_WIDTH, Program.INV_SLOT_HEIGHT) },
                { new Weapon() { WeaponID = WeaponEnumeration.G19_Handgun, Attachments = AttachmentsFlag.Second | AttachmentsFlag.Third }, new System.Drawing.Rectangle(Program.INV_SLOT_WIDTH * ++weaponColumnInc, Program.INV_SLOT_HEIGHT * weaponRowInc, Program.INV_SLOT_WIDTH, Program.INV_SLOT_HEIGHT) },
                { new Weapon() { WeaponID = WeaponEnumeration.G19_Handgun, Attachments = AttachmentsFlag.Second }, new System.Drawing.Rectangle(Program.INV_SLOT_WIDTH * ++weaponColumnInc, Program.INV_SLOT_HEIGHT * weaponRowInc, Program.INV_SLOT_WIDTH, Program.INV_SLOT_HEIGHT) },
                { new Weapon() { WeaponID = WeaponEnumeration.G19_Handgun, Attachments = AttachmentsFlag.First | AttachmentsFlag.Second }, new System.Drawing.Rectangle(Program.INV_SLOT_WIDTH * (weaponColumnInc = 10), Program.INV_SLOT_HEIGHT * weaponRowInc, Program.INV_SLOT_WIDTH * 2, Program.INV_SLOT_HEIGHT) },
                { new Weapon() { WeaponID = WeaponEnumeration.Samurai_Edge, Attachments = AttachmentsFlag.None }, new System.Drawing.Rectangle(Program.INV_SLOT_WIDTH * (weaponColumnInc = 12), Program.INV_SLOT_HEIGHT * weaponRowInc, Program.INV_SLOT_WIDTH, Program.INV_SLOT_HEIGHT) },
                { new Weapon() { WeaponID = WeaponEnumeration.G18_Handgun, Attachments = AttachmentsFlag.None }, new System.Drawing.Rectangle(Program.INV_SLOT_WIDTH * (weaponColumnInc = 16), Program.INV_SLOT_HEIGHT * weaponRowInc, Program.INV_SLOT_WIDTH, Program.INV_SLOT_HEIGHT) },
                { new Weapon() { WeaponID = WeaponEnumeration.G18_Burst_Handgun, Attachments = AttachmentsFlag.None }, new System.Drawing.Rectangle(Program.INV_SLOT_WIDTH * ++weaponColumnInc, Program.INV_SLOT_HEIGHT * weaponRowInc, Program.INV_SLOT_WIDTH, Program.INV_SLOT_HEIGHT) },

                // Row 2.
                { new Weapon() { WeaponID = WeaponEnumeration.Shotgun, Attachments = AttachmentsFlag.None }, new System.Drawing.Rectangle(Program.INV_SLOT_WIDTH * (weaponColumnInc = 0), Program.INV_SLOT_HEIGHT * ++weaponRowInc, Program.INV_SLOT_WIDTH, Program.INV_SLOT_HEIGHT) },
                { new Weapon() { WeaponID = WeaponEnumeration.Shotgun, Attachments = AttachmentsFlag.First }, new System.Drawing.Rectangle(Program.INV_SLOT_WIDTH * ++weaponColumnInc, Program.INV_SLOT_HEIGHT * weaponRowInc, Program.INV_SLOT_WIDTH * 2, Program.INV_SLOT_HEIGHT) },
                { new Weapon() { WeaponID = WeaponEnumeration.Shotgun, Attachments = AttachmentsFlag.First | AttachmentsFlag.Third }, new System.Drawing.Rectangle(Program.INV_SLOT_WIDTH * (weaponColumnInc = 3), Program.INV_SLOT_HEIGHT * weaponRowInc, Program.INV_SLOT_WIDTH * 2, Program.INV_SLOT_HEIGHT) },
                { new Weapon() { WeaponID = WeaponEnumeration.Shotgun, Attachments = AttachmentsFlag.First | AttachmentsFlag.Second | AttachmentsFlag.Third }, new System.Drawing.Rectangle(Program.INV_SLOT_WIDTH * (weaponColumnInc = 5), Program.INV_SLOT_HEIGHT * weaponRowInc, Program.INV_SLOT_WIDTH * 2, Program.INV_SLOT_HEIGHT) },
                { new Weapon() { WeaponID = WeaponEnumeration.Shotgun, Attachments = AttachmentsFlag.Third }, new System.Drawing.Rectangle(Program.INV_SLOT_WIDTH * (weaponColumnInc = 7), Program.INV_SLOT_HEIGHT * weaponRowInc, Program.INV_SLOT_WIDTH, Program.INV_SLOT_HEIGHT) },
                { new Weapon() { WeaponID = WeaponEnumeration.Shotgun, Attachments = AttachmentsFlag.Second | AttachmentsFlag.Third }, new System.Drawing.Rectangle(Program.INV_SLOT_WIDTH * ++weaponColumnInc, Program.INV_SLOT_HEIGHT * weaponRowInc, Program.INV_SLOT_WIDTH, Program.INV_SLOT_HEIGHT) },
                { new Weapon() { WeaponID = WeaponEnumeration.Shotgun, Attachments = AttachmentsFlag.Second }, new System.Drawing.Rectangle(Program.INV_SLOT_WIDTH * ++weaponColumnInc, Program.INV_SLOT_HEIGHT * weaponRowInc, Program.INV_SLOT_WIDTH, Program.INV_SLOT_HEIGHT) },
                { new Weapon() { WeaponID = WeaponEnumeration.Shotgun, Attachments = AttachmentsFlag.First | AttachmentsFlag.Second }, new System.Drawing.Rectangle(Program.INV_SLOT_WIDTH * (weaponColumnInc = 10), Program.INV_SLOT_HEIGHT * weaponRowInc, Program.INV_SLOT_WIDTH * 2, Program.INV_SLOT_HEIGHT) },
                { new Weapon() { WeaponID = WeaponEnumeration.Lightning_Hawk, Attachments = AttachmentsFlag.None }, new System.Drawing.Rectangle(Program.INV_SLOT_WIDTH * (weaponColumnInc = 12), Program.INV_SLOT_HEIGHT * weaponRowInc, Program.INV_SLOT_WIDTH, Program.INV_SLOT_HEIGHT) },
                { new Weapon() { WeaponID = WeaponEnumeration.Lightning_Hawk, Attachments = AttachmentsFlag.Second }, new System.Drawing.Rectangle(Program.INV_SLOT_WIDTH * ++weaponColumnInc, Program.INV_SLOT_HEIGHT * weaponRowInc, Program.INV_SLOT_WIDTH, Program.INV_SLOT_HEIGHT) },

                // Row 3.
                { new Weapon() { WeaponID = WeaponEnumeration.CQBR_Assault_Rifle, Attachments = AttachmentsFlag.None }, new System.Drawing.Rectangle(Program.INV_SLOT_WIDTH * (weaponColumnInc = 0), Program.INV_SLOT_HEIGHT * ++weaponRowInc, Program.INV_SLOT_WIDTH * 2, Program.INV_SLOT_HEIGHT) },
                { new Weapon() { WeaponID = WeaponEnumeration.CQBR_Assault_Rifle, Attachments = AttachmentsFlag.First }, new System.Drawing.Rectangle(Program.INV_SLOT_WIDTH * (weaponColumnInc = 2), Program.INV_SLOT_HEIGHT * weaponRowInc, Program.INV_SLOT_WIDTH * 2, Program.INV_SLOT_HEIGHT) },
                { new Weapon() { WeaponID = WeaponEnumeration.CQBR_Assault_Rifle, Attachments = AttachmentsFlag.First | AttachmentsFlag.Third }, new System.Drawing.Rectangle(Program.INV_SLOT_WIDTH * (weaponColumnInc = 4), Program.INV_SLOT_HEIGHT * weaponRowInc, Program.INV_SLOT_WIDTH * 2, Program.INV_SLOT_HEIGHT) },
                { new Weapon() { WeaponID = WeaponEnumeration.CQBR_Assault_Rifle, Attachments = AttachmentsFlag.First | AttachmentsFlag.Second | AttachmentsFlag.Third }, new System.Drawing.Rectangle(Program.INV_SLOT_WIDTH * (weaponColumnInc = 6), Program.INV_SLOT_HEIGHT * weaponRowInc, Program.INV_SLOT_WIDTH * 2, Program.INV_SLOT_HEIGHT) },
                { new Weapon() { WeaponID = WeaponEnumeration.CQBR_Assault_Rifle, Attachments = AttachmentsFlag.Third }, new System.Drawing.Rectangle(Program.INV_SLOT_WIDTH * (weaponColumnInc = 8), Program.INV_SLOT_HEIGHT * weaponRowInc, Program.INV_SLOT_WIDTH * 2, Program.INV_SLOT_HEIGHT) },
                { new Weapon() { WeaponID = WeaponEnumeration.CQBR_Assault_Rifle, Attachments = AttachmentsFlag.Second | AttachmentsFlag.Third }, new System.Drawing.Rectangle(Program.INV_SLOT_WIDTH * (weaponColumnInc = 10), Program.INV_SLOT_HEIGHT * weaponRowInc, Program.INV_SLOT_WIDTH * 2, Program.INV_SLOT_HEIGHT) },
                { new Weapon() { WeaponID = WeaponEnumeration.CQBR_Assault_Rifle, Attachments = AttachmentsFlag.Second }, new System.Drawing.Rectangle(Program.INV_SLOT_WIDTH * (weaponColumnInc = 12), Program.INV_SLOT_HEIGHT * weaponRowInc, Program.INV_SLOT_WIDTH * 2, Program.INV_SLOT_HEIGHT) },
                { new Weapon() { WeaponID = WeaponEnumeration.CQBR_Assault_Rifle, Attachments = AttachmentsFlag.First | AttachmentsFlag.Second }, new System.Drawing.Rectangle(Program.INV_SLOT_WIDTH * (weaponColumnInc = 14), Program.INV_SLOT_HEIGHT * weaponRowInc, Program.INV_SLOT_WIDTH * 2, Program.INV_SLOT_HEIGHT) },


                // Row 4.
                { new Weapon() { WeaponID = WeaponEnumeration.Infinite_Rocket_Launcher, Attachments = AttachmentsFlag.None }, new System.Drawing.Rectangle(Program.INV_SLOT_WIDTH * (weaponColumnInc = 6), Program.INV_SLOT_HEIGHT * ++weaponRowInc, Program.INV_SLOT_WIDTH * 2, Program.INV_SLOT_HEIGHT) },
                { new Weapon() { WeaponID = WeaponEnumeration.Infinite_CQBR_Assault_Rifle, Attachments = AttachmentsFlag.None }, new System.Drawing.Rectangle(Program.INV_SLOT_WIDTH * (weaponColumnInc = 8), Program.INV_SLOT_HEIGHT * weaponRowInc, Program.INV_SLOT_WIDTH * 2, Program.INV_SLOT_HEIGHT) },

                // Row 5.
                { new Weapon() { WeaponID = WeaponEnumeration.Combat_Knife_Carlos, Attachments = AttachmentsFlag.None }, new System.Drawing.Rectangle(Program.INV_SLOT_WIDTH * (weaponColumnInc = 0), Program.INV_SLOT_HEIGHT * ++weaponRowInc, Program.INV_SLOT_WIDTH, Program.INV_SLOT_HEIGHT) },
                { new Weapon() { WeaponID = WeaponEnumeration.Survival_Knife_Jill, Attachments = AttachmentsFlag.None }, new System.Drawing.Rectangle(Program.INV_SLOT_WIDTH * ++weaponColumnInc, Program.INV_SLOT_HEIGHT * weaponRowInc, Program.INV_SLOT_WIDTH, Program.INV_SLOT_HEIGHT) },
                { new Weapon() { WeaponID = WeaponEnumeration.Infinite_MUP_Handgun, Attachments = AttachmentsFlag.None }, new System.Drawing.Rectangle(Program.INV_SLOT_WIDTH * (weaponColumnInc = 4), Program.INV_SLOT_HEIGHT * weaponRowInc, Program.INV_SLOT_WIDTH, Program.INV_SLOT_HEIGHT) },
                { new Weapon() { WeaponID = WeaponEnumeration.RAIDEN, Attachments = AttachmentsFlag.None }, new System.Drawing.Rectangle(Program.INV_SLOT_WIDTH * ++weaponColumnInc, Program.INV_SLOT_HEIGHT * weaponRowInc, Program.INV_SLOT_WIDTH * 2, Program.INV_SLOT_HEIGHT) },
                { new Weapon() { WeaponID = WeaponEnumeration.HOT_DOGGER, Attachments = AttachmentsFlag.None }, new System.Drawing.Rectangle(Program.INV_SLOT_WIDTH * (weaponColumnInc = 7), Program.INV_SLOT_HEIGHT * weaponRowInc, Program.INV_SLOT_WIDTH, Program.INV_SLOT_HEIGHT) },
                { new Weapon() { WeaponID = WeaponEnumeration.Hand_Grenade, Attachments = AttachmentsFlag.None }, new System.Drawing.Rectangle(Program.INV_SLOT_WIDTH * (weaponColumnInc = 9), Program.INV_SLOT_HEIGHT * weaponRowInc, Program.INV_SLOT_WIDTH, Program.INV_SLOT_HEIGHT) },
                { new Weapon() { WeaponID = WeaponEnumeration.Flash_Grenade, Attachments = AttachmentsFlag.None }, new System.Drawing.Rectangle(Program.INV_SLOT_WIDTH * ++weaponColumnInc, Program.INV_SLOT_HEIGHT * weaponRowInc, Program.INV_SLOT_WIDTH, Program.INV_SLOT_HEIGHT) },
                { new Weapon() { WeaponID = WeaponEnumeration.Grenade_Launcher, Attachments = AttachmentsFlag.None }, new System.Drawing.Rectangle(Program.INV_SLOT_WIDTH * ++weaponColumnInc, Program.INV_SLOT_HEIGHT * weaponRowInc, Program.INV_SLOT_WIDTH * 2, Program.INV_SLOT_HEIGHT) },

            };
        }
    }
}
