﻿using SRTPluginProviderRE3;
using SRTPluginProviderRE3.Structs;
using SRTPluginProviderRE3.Structs.GameStructs;
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

            if (Program.config.NoTitlebar)
                this.FormBorderStyle = FormBorderStyle.None;

            if (Program.config.Transparent)
                this.TransparencyKey = Color.Black;

            // Only run the following code if we're rendering inventory.
            if (!Program.config.NoInventory)
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
            if (Program.config.ScalingFactor != 1d)
            {
                try
                {
                    inventoryItemImage = new Bitmap(inventoryItemImage, (int)Math.Round(inventoryItemImage.Width * Program.config.ScalingFactor, MidpointRounding.AwayFromZero), (int)Math.Round(inventoryItemImage.Height * Program.config.ScalingFactor, MidpointRounding.AwayFromZero));
                    inventoryWeaponImage = new Bitmap(inventoryWeaponImage, (int)Math.Round(inventoryWeaponImage.Width * Program.config.ScalingFactor, MidpointRounding.AwayFromZero), (int)Math.Round(inventoryWeaponImage.Height * Program.config.ScalingFactor, MidpointRounding.AwayFromZero));
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

        // Player HP change tracking
        private int previousHealth = -1;
        private Font healthFont = new Font("Consolas", 14, FontStyle.Bold);
        private Brush healthBrush = Brushes.Red;
        private string currentHP = "DEAD";
        private float healthX = 82f;
        //Inventory change tracking
        private InventoryEntry[] previousInventory = new InventoryEntry[20];
        public void ReceiveData(object gameMemory)
        {
            gameMemoryRE3 = (GameMemoryRE3)gameMemory;
            try
            {
                if (gameMemoryRE3.PlayerManager.Health.CurrentHP != previousHealth)
                {
                    previousHealth = gameMemoryRE3.PlayerManager.Health.CurrentHP;

                    // Draw health.
                    if (gameMemoryRE3.PlayerManager.Health.CurrentHP > 1200 || gameMemoryRE3.PlayerManager.Health.CurrentHP <= 0) // Dead?
                    {
                        healthBrush = Brushes.Red;
                        healthX = 82f;
                        currentHP = "DEAD";
                        this.playerHealthStatus.ThreadSafeSetHealthImage(Properties.Resources.EMPTY, "EMPTY");
                    }
                    else if (gameMemoryRE3.PlayerManager.Health.CurrentHP >= 801) // Fine (Green)
                    {
                        healthBrush = Brushes.LawnGreen;
                        healthX = 15f;
                        currentHP = gameMemoryRE3.PlayerManager.Health.CurrentHP.ToString();
                        this.playerHealthStatus.ThreadSafeSetHealthImage(Properties.Resources.FINE, "FINE");
                    }
                    else if (gameMemoryRE3.PlayerManager.Health.CurrentHP <= 800 && gameMemoryRE3.PlayerManager.Health.CurrentHP >= 361) // Caution (Yellow)
                    {
                        healthBrush = Brushes.Goldenrod;
                        healthX = 15f;
                        currentHP = gameMemoryRE3.PlayerManager.Health.CurrentHP.ToString();
                        this.playerHealthStatus.ThreadSafeSetHealthImage(Properties.Resources.CAUTION_YELLOW, "CAUTION_YELLOW");
                    }
                    else if (gameMemoryRE3.PlayerManager.Health.CurrentHP <= 360) // Danger (Red)
                    {
                        healthBrush = Brushes.Red;
                        healthX = 15f;
                        currentHP = gameMemoryRE3.PlayerManager.Health.CurrentHP.ToString();
                        this.playerHealthStatus.ThreadSafeSetHealthImage(Properties.Resources.DANGER, "DANGER");
                    }

                    this.playerHealthStatus.Invalidate();
                }
                if (!Program.config.NoInventory)
                {
                    if (!gameMemoryRE3.Items.SequenceEqual(previousInventory))
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
        
        // This paint method gets called a lot more frequently than the others either because it is a PictureBox or because the image is a Gif. Either way, do NOT do logic in here.
        private void playerHealthStatus_Paint(object sender, PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = smoothingMode;
            e.Graphics.CompositingQuality = compositingQuality;
            e.Graphics.CompositingMode = compositingMode;
            e.Graphics.InterpolationMode = interpolationMode;
            e.Graphics.PixelOffsetMode = pixelOffsetMode;
            e.Graphics.TextRenderingHint = textRenderingHint;

            e.Graphics.DrawString(currentHP, healthFont, healthBrush, healthX, 37, stdStringFormat);
        }

        private void inventoryPanel_Paint(object sender, PaintEventArgs e)
        {
            //if (previousInventory[0].Data == null)
            //{
            //    for (int i = 0; i < previousInventory.Length; ++i)
            //    {
            //        previousInventory[i].SlotNo = i;
            //        previousInventory[i].Data = new int[5]
            //        {
            //            0x00000000,
            //            unchecked((int)0xFFFFFFFF),
            //            0x00000000,
            //            0x00000000,
            //            0x01000000
            //        };
            //        previousInventory[i].InvDataOffset = 0L;
            //    }
            //}

            if (!Program.config.NoInventory && gameMemoryRE3.Items != null)
            {
                e.Graphics.SmoothingMode = smoothingMode;
                e.Graphics.CompositingQuality = compositingQuality;
                e.Graphics.CompositingMode = compositingMode;
                e.Graphics.InterpolationMode = interpolationMode;
                e.Graphics.PixelOffsetMode = pixelOffsetMode;
                e.Graphics.TextRenderingHint = textRenderingHint;

                for (int i = 0; i < gameMemoryRE3.Items.Length; ++i)
                {
                    // Only do logic for non-blank and non-broken items.
                    //if (gameMemoryRE3.Items[i] != default && gameMemoryRE3.Items[i].SlotNo >= 0 && gameMemoryRE3.Items[i].SlotNo <= 19 && !gameMemoryRE3.Items[i].IsEmptySlot)
                    if (gameMemoryRE3.Items[i].SlotNo >= 0 && gameMemoryRE3.Items[i].SlotNo <= 19 && !gameMemoryRE3.Items[i].IsEmptySlot)
                    {

                        int slotColumn = gameMemoryRE3.Items[i].SlotNo % 4;
                        int slotRow = gameMemoryRE3.Items[i].SlotNo / 4;
                        int imageX = slotColumn * Program.INV_SLOT_WIDTH;
                        int imageY = slotRow * Program.INV_SLOT_HEIGHT;
                        int textX = imageX + Program.INV_SLOT_WIDTH;
                        int textY = imageY + Program.INV_SLOT_HEIGHT;
                        bool evenSlotColumn = slotColumn % 2 == 0;
                        Brush textBrush = Brushes.White;
                        if (gameMemoryRE3.Items[i].Count == 0)
                            textBrush = Brushes.DarkRed;

                        TextureBrush imageBrush;
                        Weapon weapon;
                        if (gameMemoryRE3.Items[i].IsItem && Program.ItemToImageBrush.ContainsKey(gameMemoryRE3.Items[i].ItemId))
                            imageBrush = Program.ItemToImageBrush[gameMemoryRE3.Items[i].ItemId];
                        else if (gameMemoryRE3.Items[i].IsWeapon && Program.WeaponToImageBrush.ContainsKey(weapon = new Weapon() { WeaponID = gameMemoryRE3.Items[i].WeaponId, Attachments = gameMemoryRE3.Items[i].WeaponParts }))
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

                            textX += Program.INV_SLOT_WIDTH;
                        }

                        e.Graphics.FillRectangle(imageBrush, imageX, imageY, imageBrush.Image.Width, imageBrush.Image.Height);
                        e.Graphics.DrawString((gameMemoryRE3.Items[i].Count != -1) ? gameMemoryRE3.Items[i].Count.ToString() : "∞", new Font("Consolas", 14, FontStyle.Bold), textBrush, textX, textY, invStringFormat);
                    }

                    //previousInventory[i] = gameMemoryRE3.Items[i].Clone();
                    previousInventory[i] = gameMemoryRE3.Items[i];
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
            e.Graphics.DrawString(string.Format("{0}", gameMemoryRE3.Timer.IGTFormattedString), new Font("Consolas", 16, FontStyle.Bold), Brushes.White, 0, 0, stdStringFormat);

            if (Program.config.Debug)
            {
                e.Graphics.DrawString("Raw IGT", new Font("Consolas", 9, FontStyle.Bold), Brushes.Gray, 0, 25, stdStringFormat);
                e.Graphics.DrawString("A:" + gameMemoryRE3.Timer.GameSaveData.GameElapsedTime.ToString("00000000000000000000"), new Font("Consolas", 9, FontStyle.Bold), (gameMemoryRE3.Timer.MeasureGameElapsedTime) ? Brushes.DarkRed : Brushes.Gray, 0, 38, stdStringFormat);
                e.Graphics.DrawString("C:" + gameMemoryRE3.Timer.GameSaveData.DemoSpendingTime.ToString("00000000000000000000"), new Font("Consolas", 9, FontStyle.Bold), (gameMemoryRE3.Timer.MeasureDemoSpendingTime) ? Brushes.DarkRed : Brushes.Gray, 0, 53, stdStringFormat);
                e.Graphics.DrawString("M:" + gameMemoryRE3.Timer.GameSaveData.InventorySpendingTime.ToString("00000000000000000000"), new Font("Consolas", 9, FontStyle.Bold), (gameMemoryRE3.Timer.MeasureInventorySpendingTime) ? Brushes.DarkRed : Brushes.Gray, 0, 68, stdStringFormat);
                e.Graphics.DrawString("P:" + gameMemoryRE3.Timer.GameSaveData.PauseSpendingTime.ToString("00000000000000000000"), new Font("Consolas", 9, FontStyle.Bold), (gameMemoryRE3.Timer.MeasurePauseSpendingTime) ? Brushes.DarkRed : Brushes.Gray, 0, 83, stdStringFormat);
                heightOffset = 70; // Adding an additional offset to accomdate Raw IGT.
            }

            e.Graphics.DrawString(string.Format("DA Rank: {0}", gameMemoryRE3.RankManager.GameRank), new Font("Consolas", 9, FontStyle.Bold), Brushes.Gray, 0, heightOffset + (heightGap * ++i), stdStringFormat);
            e.Graphics.DrawString(string.Format("DA Score: {0}", gameMemoryRE3.RankManager.RankPoint), new Font("Consolas", 9, FontStyle.Bold), Brushes.Gray, 0, heightOffset + (heightGap * ++i), stdStringFormat);

            //if (Program.config.Debug)
            //    e.Graphics.DrawString(string.Format("Frame Delta: {0}", gameMemoryRE3.FrameDelta), new Font("Consolas", 9, FontStyle.Bold), Brushes.Gray, 0, heightOffset + (heightGap * ++i), stdStringFormat);

            if (Program.config.Debug)
                e.Graphics.DrawString(string.Format("Map ID: {0}", gameMemoryRE3.MapID), new Font("Consolas", 9, FontStyle.Bold), Brushes.Gray, 0, heightOffset + (heightGap * ++i), stdStringFormat);

            //if (Program.config.Debug)
            //    e.Graphics.DrawString(string.Format("Saves: {0}", gameMemoryRE3.Saves), new Font("Consolas", 9, FontStyle.Bold), Brushes.Gray, 0, heightOffset + (heightGap * ++i), stdStringFormat);

            e.Graphics.DrawString("Enemy HP", new Font("Consolas", 10, FontStyle.Bold), Brushes.Red, 0, heightOffset + (heightGap * ++i), stdStringFormat);
            if (gameMemoryRE3.Enemies != null)
            {
                foreach (Enemy enemy in gameMemoryRE3.Enemies.Where(a => a.IsAlive).OrderBy(a => a.Percentage).ThenByDescending(a => a.CurrentHP))
                {
                    int x = 0;
                    int y = heightOffset + (heightGap * ++i);

                    DrawProgressBarGDI(e, backBrushGDI, foreBrushGDI, x, y, 146, heightGap, enemy.Percentage * 100f, 100f);
                    e.Graphics.DrawString(string.Format("{0} {1:P1}", enemy.CurrentHP, enemy.Percentage), new Font("Consolas", 10, FontStyle.Bold), Brushes.Red, x, y, stdStringFormat);
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
            if (!Program.config.NoInventory)
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
