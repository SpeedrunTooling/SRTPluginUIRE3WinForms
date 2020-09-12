﻿using SRTPluginProviderRE3;
using SRTPluginProviderRE3.Structures;
using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace SRTPluginUIRE3WinForms
{
    public partial class MainUI : Form
    {
        // How often to perform more expensive operations.
        // 333 milliseconds for a full scan.
        // 16 milliseconds for a slim scan.
        public const long FULL_UI_DRAW_TICKS = TimeSpan.TicksPerMillisecond * 333L;
        public const double SLIM_UI_DRAW_MS = 16d;

        private long lastFullUIDraw;

        // Quality settings (high performance).
        private CompositingMode compositingMode = CompositingMode.SourceOver;
        private CompositingQuality compositingQuality = CompositingQuality.HighSpeed;
        private SmoothingMode smoothingMode = SmoothingMode.None;
        private PixelOffsetMode pixelOffsetMode = PixelOffsetMode.Half;
        private InterpolationMode interpolationMode = InterpolationMode.NearestNeighbor;
        private TextRenderingHint textRenderingHint = TextRenderingHint.AntiAliasGridFit;

        // Text alignment and formatting.
        private StringFormat invStringFormat = new StringFormat(StringFormat.GenericDefault) { Alignment = StringAlignment.Far, LineAlignment = StringAlignment.Far };
        private StringFormat stdStringFormat = new StringFormat(StringFormat.GenericDefault) { Alignment = StringAlignment.Near, LineAlignment = StringAlignment.Near };

        private Bitmap inventoryError; // An error image.
        private Bitmap inventoryItemImage;
        private Bitmap inventoryWeaponImage;
        private Matrix inventoryNormalTransform = new Matrix(1f, 0f, 0f, 1f, 0f, 0f);
        private Matrix inventoryShiftedTransform = new Matrix(1f, 0f, 0f, 1f, Program.INV_SLOT_WIDTH, 0f);

        private GameMemoryRE3 gameMemoryRE3;

        public MainUI()
        {
            InitializeComponent();

            // Set titlebar.
            this.Text = Program.srtTitle;

            this.ContextMenuStrip = SRTPluginUIRE3WinForms.contextMenuStrip;
            this.playerHealthStatus.ContextMenuStrip = SRTPluginUIRE3WinForms.contextMenuStrip;
            this.statisticsPanel.ContextMenuStrip = SRTPluginUIRE3WinForms.contextMenuStrip;
            this.inventoryPanel.ContextMenuStrip = SRTPluginUIRE3WinForms.contextMenuStrip;

            //GDI+
            this.playerHealthStatus.Paint += this.playerHealthStatus_Paint;
            this.statisticsPanel.Paint += this.statisticsPanel_Paint;
            this.inventoryPanel.Paint += this.inventoryPanel_Paint;

            if (Program.programSpecialOptions.Flags.HasFlag(ProgramFlags.NoTitleBar))
                this.FormBorderStyle = FormBorderStyle.None;

            if (Program.programSpecialOptions.Flags.HasFlag(ProgramFlags.Transparent))
                this.TransparencyKey = Color.Black;

            // Only run the following code if we're rendering inventory.
            if (!Program.programSpecialOptions.Flags.HasFlag(ProgramFlags.NoInventory))
            {
                GenerateImages();
                Program.GenerateBrushes(inventoryItemImage, inventoryWeaponImage, inventoryError);

                // Set the width and height of the inventory display so it matches the maximum items and the scaling size of those items.
                this.inventoryPanel.Width = Program.INV_SLOT_WIDTH * 4;
                this.inventoryPanel.Height = Program.INV_SLOT_HEIGHT * 5;

                // Adjust main form width as well.
                this.Width = this.statisticsPanel.Width + 24 + this.inventoryPanel.Width;

                // Only adjust form height if its greater than 461. We don't want it to go below this size.
                if (41 + this.inventoryPanel.Height > 461)
                    this.Height = 41 + this.inventoryPanel.Height;
            }
            else
            {
                // Disable rendering of the inventory panel.
                this.inventoryPanel.Visible = false;

                // Adjust main form width as well.
                this.Width = this.statisticsPanel.Width + 18;
            }

            lastFullUIDraw = DateTime.UtcNow.Ticks;
        }

        public void GenerateImages()
        {
            // Create a black slot image for when side-pack is not equipped.
            inventoryError = new Bitmap(Program.INV_SLOT_WIDTH, Program.INV_SLOT_HEIGHT, PixelFormat.Format32bppPArgb);
            using (Graphics grp = Graphics.FromImage(inventoryError))
            {
                grp.FillRectangle(new SolidBrush(Color.FromArgb(255, 0, 0, 0)), 0, 0, inventoryError.Width, inventoryError.Height);
                grp.DrawLine(new Pen(Color.FromArgb(150, 255, 0, 0), 3), 0, 0, inventoryError.Width, inventoryError.Height);
                grp.DrawLine(new Pen(Color.FromArgb(150, 255, 0, 0), 3), inventoryError.Width, 0, 0, inventoryError.Height);
            }

            // Transform the image into a 32-bit PARGB Bitmap.
            try
            {
                inventoryItemImage = Properties.Resources.ui0100_iam_texout.Clone(new Rectangle(0, 0, Properties.Resources.ui0100_iam_texout.Width, Properties.Resources.ui0100_iam_texout.Height), PixelFormat.Format32bppPArgb);
                inventoryWeaponImage = Properties.Resources.ui0100_wp_iam_texout.Clone(new Rectangle(0, 0, Properties.Resources.ui0100_wp_iam_texout.Width, Properties.Resources.ui0100_wp_iam_texout.Height), PixelFormat.Format32bppPArgb);
            }
            catch (Exception ex)
            {
                Program.FailFast(string.Format("[{0}] An unhandled exception has occurred. Please see below for details.\r\n\r\n[{1}] {2}\r\n{3}.\r\n\r\nPARGB Transform.", Program.srtVersion, ex.GetType().ToString(), ex.Message, ex.StackTrace), ex);
            }

            // Rescales the image down if the scaling factor is not 1.
            if (Program.programSpecialOptions.ScalingFactor != 1d)
            {
                try
                {
                    inventoryItemImage = new Bitmap(inventoryItemImage, (int)Math.Round(inventoryItemImage.Width * Program.programSpecialOptions.ScalingFactor, MidpointRounding.AwayFromZero), (int)Math.Round(inventoryItemImage.Height * Program.programSpecialOptions.ScalingFactor, MidpointRounding.AwayFromZero));
                    inventoryWeaponImage = new Bitmap(inventoryWeaponImage, (int)Math.Round(inventoryWeaponImage.Width * Program.programSpecialOptions.ScalingFactor, MidpointRounding.AwayFromZero), (int)Math.Round(inventoryWeaponImage.Height * Program.programSpecialOptions.ScalingFactor, MidpointRounding.AwayFromZero));
                }
                catch (Exception ex)
                {
                    Program.FailFast(string.Format(@"[{0}] An unhandled exception has occurred. Please see below for details.
---
[{1}] {2}
{3}", Program.srtVersion, ex.GetType().ToString(), ex.Message, ex.StackTrace), ex);
                }
            }
        }

        public void ReceiveData(object gameMemory)
        {
            gameMemoryRE3 = (GameMemoryRE3)gameMemory;
            try
            {
                if (Program.programSpecialOptions.Flags.HasFlag(ProgramFlags.AlwaysOnTop))
                {
                    bool hasFocus;
                    if (this.InvokeRequired)
                        hasFocus = PInvoke.HasActiveFocus((IntPtr)this.Invoke(new Func<IntPtr>(() => this.Handle)));
                    else
                        hasFocus = PInvoke.HasActiveFocus(this.Handle);

                    if (!hasFocus)
                    {
                        if (this.InvokeRequired)
                            this.Invoke(new Action(() => this.TopMost = true));
                        else
                            this.TopMost = true;
                    }
                }

                // Only draw occasionally, not as often as the stats panel.
                if (DateTime.UtcNow.Ticks - lastFullUIDraw >= FULL_UI_DRAW_TICKS)
                {
                    // Update the last drawn time.
                    lastFullUIDraw = DateTime.UtcNow.Ticks;

                    // Only draw these periodically to reduce CPU usage.
                    this.playerHealthStatus.Invalidate();
                    if (!Program.programSpecialOptions.Flags.HasFlag(ProgramFlags.NoInventory))
                        this.inventoryPanel.Invalidate();
                }

                // Always draw this as these are simple text draws and contains the IGT/frame count.
                this.statisticsPanel.Invalidate();
            }
            catch (Exception ex)
            {
                Debug.WriteLine("[{0}] {1}\r\n{2}", ex.GetType().ToString(), ex.Message, ex.StackTrace);
            }
        }

        private void playerHealthStatus_Paint(object sender, PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = smoothingMode;
            e.Graphics.CompositingQuality = compositingQuality;
            e.Graphics.CompositingMode = compositingMode;
            e.Graphics.InterpolationMode = interpolationMode;
            e.Graphics.PixelOffsetMode = pixelOffsetMode;
            e.Graphics.TextRenderingHint = textRenderingHint;

            // Draw health.
            Font healthFont = new Font("Consolas", 14, FontStyle.Bold);
            if (gameMemoryRE3.PlayerCurrentHealth > 1200 || gameMemoryRE3.PlayerCurrentHealth <= 0) // Dead?
            {
                e.Graphics.DrawString("DEAD", healthFont, Brushes.Red, 82, 37, stdStringFormat);
                playerHealthStatus.ThreadSafeSetHealthImage(Properties.Resources.EMPTY, "EMPTY");
            }
            else if (gameMemoryRE3.PlayerCurrentHealth >= 801) // Fine (Green)
            {
                e.Graphics.DrawString(gameMemoryRE3.PlayerCurrentHealth.ToString(), healthFont, Brushes.LawnGreen, 15, 37, stdStringFormat);
                playerHealthStatus.ThreadSafeSetHealthImage(Properties.Resources.FINE, "FINE");
            }
            else if (gameMemoryRE3.PlayerCurrentHealth <= 800 && gameMemoryRE3.PlayerCurrentHealth >= 361) // Caution (Yellow)
            {
                e.Graphics.DrawString(gameMemoryRE3.PlayerCurrentHealth.ToString(), healthFont, Brushes.Goldenrod, 15, 37, stdStringFormat);
                playerHealthStatus.ThreadSafeSetHealthImage(Properties.Resources.CAUTION_YELLOW, "CAUTION_YELLOW");
            }
            else if (gameMemoryRE3.PlayerCurrentHealth <= 360) // Danger (Red)
            {
                e.Graphics.DrawString(gameMemoryRE3.PlayerCurrentHealth.ToString(), healthFont, Brushes.Red, 15, 37, stdStringFormat);
                playerHealthStatus.ThreadSafeSetHealthImage(Properties.Resources.DANGER, "DANGER");
            }
        }

        private void inventoryPanel_Paint(object sender, PaintEventArgs e)
        {
            if (!Program.programSpecialOptions.Flags.HasFlag(ProgramFlags.NoInventory) && gameMemoryRE3.PlayerInventory != null)
            {
                e.Graphics.SmoothingMode = smoothingMode;
                e.Graphics.CompositingQuality = compositingQuality;
                e.Graphics.CompositingMode = compositingMode;
                e.Graphics.InterpolationMode = interpolationMode;
                e.Graphics.PixelOffsetMode = pixelOffsetMode;
                e.Graphics.TextRenderingHint = textRenderingHint;

                foreach (InventoryEntry inv in gameMemoryRE3.PlayerInventory)
                {
                    if (inv == default || inv.SlotPosition < 0 || inv.SlotPosition > 19 || inv.IsEmptySlot)
                        continue;

                    int slotColumn = inv.SlotPosition % 4;
                    int slotRow = inv.SlotPosition / 4;
                    int imageX = slotColumn * Program.INV_SLOT_WIDTH;
                    int imageY = slotRow * Program.INV_SLOT_HEIGHT;
                    int textX = imageX + Program.INV_SLOT_WIDTH;
                    int textY = imageY + Program.INV_SLOT_HEIGHT;
                    bool evenSlotColumn = slotColumn % 2 == 0;
                    Brush textBrush = Brushes.White;

                    if (inv.Quantity == 0)
                        textBrush = Brushes.DarkRed;

                    TextureBrush imageBrush;
                    Weapon weapon;
                    if (inv.IsItem && Program.ItemToImageBrush.ContainsKey(inv.ItemID))
                        imageBrush = Program.ItemToImageBrush[inv.ItemID];
                    else if (inv.IsWeapon && Program.WeaponToImageBrush.ContainsKey(weapon = new Weapon() { WeaponID = inv.WeaponID, Attachments = inv.Attachments }))
                        imageBrush = Program.WeaponToImageBrush[weapon];
                    else
                        imageBrush = Program.ErrorToImageBrush;
                    

                    // Double-slot item.
                    if (imageBrush.Image.Width == Program.INV_SLOT_WIDTH * 2)
                    {
                        // If we're an odd column, we need to adjust the transform so the image doesn't get split in half and tiled. Not sure why it does this.
                        if (!evenSlotColumn)
                            imageBrush.Transform = inventoryShiftedTransform; //imageBrush.TranslateTransform(Program.INV_SLOT_WIDTH, 0); // 1 0 0 1 0 0 -> 1 0 0 1 84 0
                        else
                            imageBrush.Transform = inventoryNormalTransform; // Since we're working on references, not copies, the value could be preserved from a prior Shifted Transform so let's reset.

                        // Shift the quantity text over into the 2nd slot's area.
                        textX += Program.INV_SLOT_WIDTH;
                    }

                    e.Graphics.FillRectangle(imageBrush, imageX, imageY, imageBrush.Image.Width, imageBrush.Image.Height);
                    e.Graphics.DrawString((inv.Quantity != -1) ? inv.Quantity.ToString() : "∞", new Font("Consolas", 14, FontStyle.Bold), textBrush, textX, textY, invStringFormat);
                }
            }
        }

        private void statisticsPanel_Paint(object sender, PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = smoothingMode;
            e.Graphics.CompositingQuality = compositingQuality;
            e.Graphics.CompositingMode = compositingMode;
            e.Graphics.InterpolationMode = interpolationMode;
            e.Graphics.PixelOffsetMode = pixelOffsetMode;
            e.Graphics.TextRenderingHint = textRenderingHint;

            // Additional information and stats.
            // Adjustments for displaying text properly.
            int heightGap = 15;
            int heightOffset = 0;
            int i = 1;

            // IGT Display.
            e.Graphics.DrawString(string.Format("{0}", gameMemoryRE3.IGTFormattedString), new Font("Consolas", 16, FontStyle.Bold), Brushes.White, 0, 0, stdStringFormat);

            if (Program.programSpecialOptions.Flags.HasFlag(ProgramFlags.Debug))
            {
                e.Graphics.DrawString("Raw IGT", new Font("Consolas", 9, FontStyle.Bold), Brushes.Gray, 0, 25, stdStringFormat);
                e.Graphics.DrawString("A:" + gameMemoryRE3.IGTRunningTimer.ToString("00000000000000000000"), new Font("Consolas", 9, FontStyle.Bold), (gameMemoryRE3.IsRunning) ? Brushes.DarkRed : Brushes.Gray, 0, 38, stdStringFormat);
                e.Graphics.DrawString("C:" + gameMemoryRE3.IGTCutsceneTimer.ToString("00000000000000000000"), new Font("Consolas", 9, FontStyle.Bold), (gameMemoryRE3.IsCutscene) ? Brushes.DarkRed : Brushes.Gray, 0, 53, stdStringFormat);
                e.Graphics.DrawString("M:" + gameMemoryRE3.IGTMenuTimer.ToString("00000000000000000000"), new Font("Consolas", 9, FontStyle.Bold), (gameMemoryRE3.IsMenu) ? Brushes.DarkRed : Brushes.Gray, 0, 68, stdStringFormat);
                e.Graphics.DrawString("P:" + gameMemoryRE3.IGTPausedTimer.ToString("00000000000000000000"), new Font("Consolas", 9, FontStyle.Bold), (gameMemoryRE3.IsPaused) ? Brushes.DarkRed : Brushes.Gray, 0, 83, stdStringFormat);
                heightOffset = 70; // Adding an additional offset to accomdate Raw IGT.
            }

            e.Graphics.DrawString(string.Format("DA Rank: {0}", gameMemoryRE3.Rank), new Font("Consolas", 9, FontStyle.Bold), Brushes.Gray, 0, heightOffset + (heightGap * ++i), stdStringFormat);
            e.Graphics.DrawString(string.Format("DA Score: {0}", gameMemoryRE3.RankScore), new Font("Consolas", 9, FontStyle.Bold), Brushes.Gray, 0, heightOffset + (heightGap * ++i), stdStringFormat);

            if (Program.programSpecialOptions.Flags.HasFlag(ProgramFlags.Debug))
                e.Graphics.DrawString(string.Format("Frame Delta: {0}", gameMemoryRE3.FrameDelta), new Font("Consolas", 9, FontStyle.Bold), Brushes.Gray, 0, heightOffset + (heightGap * ++i), stdStringFormat);

            if (Program.programSpecialOptions.Flags.HasFlag(ProgramFlags.Debug))
                e.Graphics.DrawString(string.Format("Map ID: {0}", gameMemoryRE3.MapID), new Font("Consolas", 9, FontStyle.Bold), Brushes.Gray, 0, heightOffset + (heightGap * ++i), stdStringFormat);

            if (Program.programSpecialOptions.Flags.HasFlag(ProgramFlags.Debug))
                e.Graphics.DrawString(string.Format("Saves: {0}", gameMemoryRE3.Saves), new Font("Consolas", 9, FontStyle.Bold), Brushes.Gray, 0, heightOffset + (heightGap * ++i), stdStringFormat);

            e.Graphics.DrawString("Enemy HP", new Font("Consolas", 10, FontStyle.Bold), Brushes.Red, 0, heightOffset + (heightGap * ++i), stdStringFormat);
            if (gameMemoryRE3.EnemyHealth != null)
            {
                foreach (EnemyHP enemyHP in gameMemoryRE3.EnemyHealth.Where(a => a.IsAlive).OrderBy(a => a.Percentage).ThenByDescending(a => a.CurrentHP))
                {
                    int x = 0;
                    int y = heightOffset + (heightGap * ++i);

                    DrawProgressBarGDI(e, backBrushGDI, foreBrushGDI, x, y, 146, heightGap, enemyHP.Percentage * 100f, 100f);
                    e.Graphics.DrawString(string.Format("{0} {1:P1}", enemyHP.CurrentHP, enemyHP.Percentage), new Font("Consolas", 10, FontStyle.Bold), Brushes.Red, x, y, stdStringFormat);
                }
            }
        }

        // Customisation in future?
        private Brush backBrushGDI = new SolidBrush(Color.FromArgb(255, 60, 60, 60));
        private Brush foreBrushGDI = new SolidBrush(Color.FromArgb(255, 100, 0, 0));

        private void DrawProgressBarGDI(PaintEventArgs e, Brush bgBrush, Brush foreBrush, float x, float y, float width, float height, float value, float maximum = 100)
        {
            // Draw BG.
            e.Graphics.DrawRectangles(new Pen(bgBrush, 2f), new RectangleF[1] { new RectangleF(x, y, width, height) });

            // Draw FG.
            RectangleF foreRect = new RectangleF(
                x + 1f,
                y + 1f,
                (width * value / maximum) - 2f,
                height - 2f
                );
            e.Graphics.FillRectangle(foreBrush, foreRect);
        }

        private void inventoryPanel_MouseDown(object sender, MouseEventArgs e)
        {
            if (!Program.programSpecialOptions.Flags.HasFlag(ProgramFlags.NoInventory))
                if (e.Button == MouseButtons.Left)
                    PInvoke.DragControl(((DoubleBufferedPanel)sender).Parent.Handle);
        }

        private void statisticsPanel_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
                PInvoke.DragControl(((DoubleBufferedPanel)sender).Parent.Handle);
        }

        private void playerHealthStatus_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
                PInvoke.DragControl(((PictureBox)sender).Parent.Handle);
        }

        private void MainUI_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
                PInvoke.DragControl(((Form)sender).Handle);
        }

        private void MainUI_Load(object sender, EventArgs e)
        {

        }

        private void MainUI_FormClosed(object sender, FormClosedEventArgs e)
        {

        }

        private void CloseForm()
        {
            if (this.InvokeRequired)
            {
                this.Invoke(new Action(() =>
                {
                    this.Close();
                }));
            }
            else
                this.Close();
        }
    }
}
