using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Windows.Forms;

namespace CrosswordPuzzle
{
    // ===================================================================
    //  DATA MODELS
    // ===================================================================
    public enum Direction { Across, Down }

    public class WordEntry
    {
        public string    Word;
        public int       Row, Col;
        public Direction Dir;
        public int       Number;
        public string    Clue;

        public WordEntry(string word, int row, int col, Direction dir, int number, string clue)
        {
            Word   = word;
            Row    = row;
            Col    = col;
            Dir    = dir;
            Number = number;
            Clue   = clue;
        }
    }

    public class PuzzleLevel
    {
        public string          Title;
        public List<WordEntry> Words;
        public int             Rows, Cols;

        public PuzzleLevel(string title, List<WordEntry> words, int rows, int cols)
        {
            Title = title;
            Words = words;
            Rows  = rows;
            Cols  = cols;
        }
    }

    public class CellTag
    {
        public int    Level, Row, Col;
        public string NumLabel;

        public CellTag(int lv, int r, int c)
        {
            Level    = lv;
            Row      = r;
            Col      = c;
            NumLabel = "";
        }
    }

    // ===================================================================
    //  COSMIC BACKGROUND PANEL
    // ===================================================================
    public class CosmicPanel : Panel
    {
        private Image _bg;
        public CosmicPanel(Image bg) { _bg = bg; DoubleBuffered = true; }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            if (_bg != null)
                e.Graphics.DrawImage(_bg, 0, 0, Width, Height);
            else
                e.Graphics.Clear(Color.FromArgb(10, 8, 35));
        }
    }

    // ===================================================================
    //  COSMIC BUTTON
    // ===================================================================
    public class CosmicButton : Button
    {
        private bool _hovered = false;
        private Color _base;
        private Color _hover;

        public CosmicButton(string text, Color baseColor, Color hoverColor)
        {
            _base  = baseColor;
            _hover = hoverColor;
            Text      = text;
            FlatStyle = FlatStyle.Flat;
            FlatAppearance.BorderSize  = 1;
            FlatAppearance.BorderColor = Color.FromArgb(200, 170, 90);
            BackColor = _base;
            ForeColor = Color.FromArgb(255, 240, 200);
            Cursor    = Cursors.Hand;
            Font      = new Font("Palatino Linotype", 10f, FontStyle.Bold);
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.DoubleBuffer, true);
        }

        protected override void OnMouseEnter(EventArgs e) { _hovered = true;  BackColor = _hover; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { _hovered = false; BackColor = _base;  Invalidate(); base.OnMouseLeave(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            using (var brush = new LinearGradientBrush(ClientRectangle,
                Color.FromArgb(180, BackColor), Color.FromArgb(220, BackColor), 90f))
                g.FillRectangle(brush, ClientRectangle);

            // Gold border
            using (var pen = new Pen(Color.FromArgb(200, 170, 90), 1))
                g.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);

            // Text
            StringFormat sf = new StringFormat();
            sf.Alignment     = StringAlignment.Center;
            sf.LineAlignment = StringAlignment.Center;
            using (var brush = new SolidBrush(ForeColor))
                g.DrawString(Text, Font, brush, ClientRectangle, sf);
        }
    }

    // ===================================================================
    //  STYLED CELL TEXTBOX
    // ===================================================================
    public class CellTextBox : TextBox
    {
        public CellTextBox()
        {
            BorderStyle     = BorderStyle.None;
            TextAlign       = HorizontalAlignment.Center;
            CharacterCasing = CharacterCasing.Upper;
            MaxLength       = 1;
            BackColor       = Color.FromArgb(60, 40, 100);
            ForeColor       = Color.FromArgb(255, 240, 200);
        }

        protected override void WndProc(ref Message m)
        {
            base.WndProc(ref m);
        }
    }

    // ===================================================================
    //  MAIN FORM
    // ===================================================================
    public class CrosswordForm : Form
    {
        // ---- Theme colors ----
        private Color C_Deep      = Color.FromArgb(10,   8,  35);
        private Color C_Purple    = Color.FromArgb(60,  35, 110);
        private Color C_PurpleMid = Color.FromArgb(80,  50, 140);
        private Color C_Gold      = Color.FromArgb(200, 170,  90);
        private Color C_GoldLight = Color.FromArgb(240, 210, 130);
        private Color C_CellBg    = Color.FromArgb(55,  35,  95);
        private Color C_CellHL    = Color.FromArgb(90,  60, 160);
        private Color C_Correct   = Color.FromArgb(40,  120,  70);
        private Color C_Wrong     = Color.FromArgb(140,  40,  50);
        private Color C_TextLight = Color.FromArgb(240, 220, 255);
        private Color C_CluePanel = Color.FromArgb(30,  18,  60);

        // ---- Fonts ----
        private Font F_Cell   = new Font("Consolas",          18f,  FontStyle.Bold);
        private Font F_Clue   = new Font("Palatino Linotype", 11f,  FontStyle.Regular);
        private Font F_Title  = new Font("Palatino Linotype", 18f,  FontStyle.Bold | FontStyle.Italic);
        private Font F_Btn    = new Font("Palatino Linotype", 10f,  FontStyle.Bold);
        private Font F_Num    = new Font("Segoe UI",           8f,   FontStyle.Bold);
        private Font F_Tab    = new Font("Palatino Linotype", 11f,  FontStyle.Bold);
        private Font F_Answer = new Font("Consolas",          14f,  FontStyle.Bold);
        private Font F_Header = new Font("Palatino Linotype", 12f,  FontStyle.Bold);

        // ---- Layout ----
        private TabControl     tabControl;
        private TabPage[]      levelTabs   = new TabPage[2];
        private Panel[]        cluesPanels = new Panel[2];
        private TextBox[]      answerBoxes = new TextBox[2];
        private TextBox[,,]    cells       = new TextBox[2, 20, 25];
        private PuzzleLevel[]  levels;
        private Image          bgImage;

        // ---- Selection state ----
        private int[]    selRow     = { -1, -1 };
        private int[]    selCol     = { -1, -1 };
        private string[] selDir     = { "",   "" };
        private int[]    selWordIdx = { -1,  -1 };

        // ===============================================================
        public CrosswordForm()
        {
            BuildPuzzleData();
            LoadBackground();
            SetupForm();
        }

        private void LoadBackground()
        {
            // Try to load pickavatarbg.png from the same directory as the exe
            string exeDir = Path.GetDirectoryName(Application.ExecutablePath);
            string[] candidates = new string[] {
                Path.Combine(exeDir, "pickavatarbg.png"),
                Path.Combine(exeDir, "..", "pickavatarbg.png"),
                Path.Combine(Directory.GetCurrentDirectory(), "pickavatarbg.png"),
                "pickavatarbg.png"
            };
            foreach (string p in candidates)
            {
                if (File.Exists(p))
                {
                    try { bgImage = Image.FromFile(p); break; }
                    catch { /* ignore */ }
                }
            }
        }

        // ===============================================================
        //  PUZZLE DATA
        // ===============================================================
        private void BuildPuzzleData()
        {
            levels = new PuzzleLevel[2];

            var l1 = new List<WordEntry>();
            // Across words
            l1.Add(new WordEntry("PAINTING",  0, 0, Direction.Across, 1,
                "The application of pigment on a flat two-dimensional surface."));
            l1.Add(new WordEntry("PINK",      3, 0, Direction.Across, 2,
                "The color that symbolizes love."));
            l1.Add(new WordEntry("BLUE",      2, 8, Direction.Across, 3,
                "The color that symbolizes calmness and peace."));
            l1.Add(new WordEntry("DIGITAL",   6, 0, Direction.Across, 4,
                "Art made with the assistance of electronic devices."));
            l1.Add(new WordEntry("SCULPTURE", 7, 6, Direction.Across, 5,
                "The three-dimensional art form involving carving and sculpting materials."));
            // Down words
            l1.Add(new WordEntry("ARTIST",    0, 1, Direction.Down, 1,
                "An art practitioner such as a painter, dancer, or writer."));
            l1.Add(new WordEntry("YELLOW",    1,13, Direction.Down, 2,
                "The three primary colors are red, blue, and this."));
            l1.Add(new WordEntry("NEUTRALS",  0, 6, Direction.Down, 3,
                "Black, white, and gray are called this because they show no color quality."));
            l1.Add(new WordEntry("MUSIC",     1,10, Direction.Down, 4,
                "The performing art whose medium is sound organized in time."));
            l1.Add(new WordEntry("DANCE",     1,16, Direction.Down, 5,
                "The rhythmic movement of the body to express an idea or emotion."));
            levels[0] = new PuzzleLevel("Level 1 - Art and Color Basics", l1, 9, 21);

            var l2 = new List<WordEntry>();
            // Across words
            l2.Add(new WordEntry("ARTISAN", 0, 0, Direction.Across, 1,
                "A craftsman who produces directly functional and decorative arts."));
            l2.Add(new WordEntry("COLOR",   2, 3, Direction.Across, 2,
                "The most expressive element of art."));
            l2.Add(new WordEntry("GREEN",   4, 3, Direction.Across, 3,
                "The color that symbolizes growth, life, and freshness."));
            l2.Add(new WordEntry("APPLIED", 6, 3, Direction.Across, 4,
                "Art that applies design and decoration to everyday objects."));
            l2.Add(new WordEntry("WHITE",   8, 0, Direction.Across, 5,
                "The color that symbolizes simplicity, purity, and innocence."));
            // Down words
            l2.Add(new WordEntry("THEATER", 0, 2, Direction.Down, 1,
                "Art that uses live performers before a live audience on a stage."));
            l2.Add(new WordEntry("FILM",    5, 7, Direction.Down, 2,
                "A series of still images that creates an illusion of moving images."));
            l2.Add(new WordEntry("BLACK",   0, 8, Direction.Down, 3,
                "The color that symbolizes despair, gloom, death, and mourning."));
            l2.Add(new WordEntry("TINT",    8, 3, Direction.Down, 4,
                "Pink is this type of color value - above the normal."));
            l2.Add(new WordEntry("SHADE",   7, 1, Direction.Down, 5,
                "Maroon is this type of color value - below the normal."));
            levels[1] = new PuzzleLevel("Level 2 - Art Forms and Shades", l2, 12, 10);
        }

        // ===============================================================
        //  FORM SETUP
        // ===============================================================
        private void SetupForm()
        {
            this.Text            = "✦ Art Crossword Puzzle ✦";
            this.Size            = new Size(1920, 1080);
            this.MinimumSize     = new Size(1280, 800);
            this.StartPosition   = FormStartPosition.CenterScreen;
            this.WindowState     = FormWindowState.Maximized;
            this.BackColor       = C_Deep;
            this.ForeColor       = C_TextLight;

            // Tab control styled
            tabControl             = new TabControl();
            tabControl.Dock        = DockStyle.Fill;
            tabControl.Font        = F_Tab;
            tabControl.Padding     = new Point(18, 6);
            tabControl.DrawMode    = TabDrawMode.OwnerDrawFixed;
            tabControl.SizeMode    = TabSizeMode.Fixed;
            tabControl.ItemSize    = new Size(160, 38);
            tabControl.DrawItem   += TabControl_DrawItem;
            tabControl.BackColor   = C_Deep;
            this.Controls.Add(tabControl);

            for (int lv = 0; lv < 2; lv++)
                BuildLevelTab(lv);
        }

        private void TabControl_DrawItem(object sender, DrawItemEventArgs e)
        {
            TabPage page = tabControl.TabPages[e.Index];
            bool    sel  = (e.Index == tabControl.SelectedIndex);

            using (var bg = new LinearGradientBrush(e.Bounds,
                sel ? C_PurpleMid : C_Purple,
                sel ? C_Purple    : C_Deep,
                90f))
                e.Graphics.FillRectangle(bg, e.Bounds);

            using (var pen = new Pen(C_Gold, 1))
                e.Graphics.DrawRectangle(pen, e.Bounds.X, e.Bounds.Y,
                    e.Bounds.Width - 1, e.Bounds.Height - 1);

            StringFormat sf = new StringFormat();
            sf.Alignment     = StringAlignment.Center;
            sf.LineAlignment = StringAlignment.Center;
            using (var brush = new SolidBrush(sel ? C_GoldLight : C_TextLight))
                e.Graphics.DrawString(page.Text, F_Tab, brush, e.Bounds, sf);
        }

        // ===============================================================
        //  BUILD LEVEL TAB
        // ===============================================================
        private void BuildLevelTab(int lv)
        {
            var tab       = new TabPage(string.Format("No. {0}", lv + 1));
            tab.BackColor = C_Deep;
            tab.Padding   = new Padding(0);
            levelTabs[lv] = tab;
            tabControl.TabPages.Add(tab);

            // Cosmic background container
            var rootPanel = bgImage != null
                ? (Panel)new CosmicPanel(bgImage)
                : new Panel();
            rootPanel.Dock      = DockStyle.Fill;
            rootPanel.BackColor = bgImage == null ? C_Deep : Color.Transparent;
            tab.Controls.Add(rootPanel);

            // Dark overlay for readability
            var overlay         = new Panel();
            overlay.Dock        = DockStyle.Fill;
            overlay.BackColor   = Color.Transparent;
            overlay.BackColor   = Color.FromArgb(40, 10, 8, 30);
            rootPanel.Controls.Add(overlay);

            // ---- Title label ----
            var titleLbl       = new Label();
            titleLbl.Text      = levels[lv].Title.ToUpper();
            titleLbl.Font      = F_Title;
            titleLbl.ForeColor = C_GoldLight;
            titleLbl.BackColor = Color.Transparent;
            titleLbl.AutoSize  = true;
            titleLbl.Location  = new Point(20, 14);
            rootPanel.Controls.Add(titleLbl);
            titleLbl.BringToFront();

            // ---- Grid sizing ----
            // Auto-detect actual used rows/cols from word data for this level
            int gridRows = 0, gridCols = 0;
            foreach (WordEntry wx in levels[lv].Words)
            {
                int maxR = wx.Row + (wx.Dir == Direction.Down   ? wx.Word.Length - 1 : 0);
                int maxC = wx.Col + (wx.Dir == Direction.Across ? wx.Word.Length - 1 : 0);
                if (maxR + 1 > gridRows) gridRows = maxR + 1;
                if (maxC + 1 > gridCols) gridCols = maxC + 1;
            }

            int screenW     = System.Windows.Forms.Screen.PrimaryScreen.WorkingArea.Width;
            int screenH     = System.Windows.Forms.Screen.PrimaryScreen.WorkingArea.Height;
            int clueReserve = 500;
            int maxGridW    = screenW - clueReserve - 80;
            int maxGridH    = screenH - 30 - 40 - 56 - 80 - 20;

            // Reference sizing always derived from Level 0 (widest level)
            int refCols = 0, refRows = 0;
            foreach (WordEntry wx in levels[0].Words)
            {
                int maxR = wx.Row + (wx.Dir == Direction.Down   ? wx.Word.Length - 1 : 0);
                int maxC = wx.Col + (wx.Dir == Direction.Across ? wx.Word.Length - 1 : 0);
                if (maxR + 1 > refRows) refRows = maxR + 1;
                if (maxC + 1 > refCols) refCols = maxC + 1;
            }
            int refCellByW  = maxGridW / refCols;
            int refCellByH  = maxGridH / refRows;
            int refCellSize = Math.Max(22, Math.Min(refCellByW, refCellByH));
            int refGridW    = refCols * refCellSize + 2; // Level 1 grid width

            // Level 1: use refCellSize (unchanged)
            // ---- Cell size calculation ----
            // Level 1: fit cols across maxGridW, rows within maxGridH -> refCellSize (unchanged)
            // Level 2: fit cols across refGridW (same box width as L1), rows within maxGridH
            //          take the SMALLER so everything is visible on screen
            int cSzW, cSzH, cSz;
            if (lv == 0)
            {
                cSz = refCellSize;
            }
            else
            {
                cSzW = (gridCols > 0) ? (refGridW - 2) / gridCols : refCellSize;
                cSzH = (gridRows  > 0) ? maxGridH      / gridRows  : refCellSize;
                cSz  = Math.Max(22, Math.Min(cSzW, cSzH));
            }

            // Actual grid pixel size
            int gridW = gridCols * cSz + 2;
            int gridH = gridRows * cSz + 2;

            // Level 1: outer box = gridW exactly
            // Level 2: outer box = refGridW (same width as Level 1's box)
            int outerW    = (lv == 0) ? gridW + 4 : refGridW + 4;
            int gridViewH = gridH + 4;

            // ---- Grid outer container with gold border (no scrollbar) ----
            var gridOuter        = new Panel();
            gridOuter.Location   = new Point(20, 56);
            gridOuter.Size       = new Size(outerW, gridViewH);
            gridOuter.BackColor  = Color.Transparent;
            gridOuter.Paint     += delegate(object s, PaintEventArgs e)
            {
                using (var bg = new SolidBrush(Color.FromArgb(200, 5, 3, 15)))
                    e.Graphics.FillRectangle(bg, 0, 0, gridOuter.Width, gridOuter.Height);
                using (var pen = new Pen(C_Gold, 2))
                    e.Graphics.DrawRectangle(pen, 0, 0,
                        gridOuter.Width - 1, gridOuter.Height - 1);
            };
            rootPanel.Controls.Add(gridOuter);
            gridOuter.BringToFront();

            // Inner grid panel — always starts at (2,2), fills the full inner area
            var gridPanel              = new Panel();
            gridPanel.Location         = new Point(2, 2);
            gridPanel.Size             = new Size(gridW, gridH);
            gridPanel.BackColor        = Color.FromArgb(255, 5, 3, 15);
            gridPanel.AutoScroll       = false;
            gridOuter.Controls.Add(gridPanel);

            // Mark used cells
            bool[,] used = new bool[gridRows, gridCols];
            foreach (WordEntry w in levels[lv].Words)
                for (int i = 0; i < w.Word.Length; i++)
                {
                    int r = w.Row + (w.Dir == Direction.Down   ? i : 0);
                    int c = w.Col + (w.Dir == Direction.Across ? i : 0);
                    used[r, c] = true;
                }

            int border = 1;

            for (int r = 0; r < gridRows; r++)
            {
                for (int c = 0; c < gridCols; c++)
                {
                    if (!used[r, c]) continue;

                    var tb             = new CellTextBox();
                    tb.Size            = new Size(cSz - border, cSz - border);
                    tb.Location        = new Point(c * cSz + border, r * cSz + border);
                    tb.Font            = new Font("Consolas", Math.Max(8f, cSz * 0.38f), FontStyle.Bold);
                    tb.BackColor       = C_CellBg;
                    tb.ForeColor       = C_TextLight;
                    tb.Tag             = new CellTag(lv, r, c);
                    tb.GotFocus       += Cell_GotFocus;
                    tb.Click          += Cell_Click;
                    tb.KeyPress       += Cell_KeyPress;
                    tb.TextChanged    += Cell_TextChanged;
                    gridPanel.Controls.Add(tb);
                    cells[lv, r, c]    = tb;
                }
            }

            // Number labels
            foreach (WordEntry w in levels[lv].Words)
            {
                TextBox startCell = cells[lv, w.Row, w.Col];
                if (startCell == null) continue;

                CellTag ctag = startCell.Tag as CellTag;
                if (ctag != null && ctag.NumLabel == "")
                {
                    var numLbl       = new Label();
                    numLbl.Text      = w.Number.ToString();
                    numLbl.Font      = F_Num;
                    numLbl.ForeColor = C_GoldLight;
                    numLbl.BackColor = Color.Transparent;
                    numLbl.AutoSize  = false;
                    numLbl.Size      = new Size(20, 16);
                    numLbl.Location  = new Point(startCell.Location.X + 2,
                                                 startCell.Location.Y + 1);
                    numLbl.Enabled   = false;
                    gridPanel.Controls.Add(numLbl);
                    numLbl.BringToFront();
                    ctag.NumLabel    = w.Number.ToString();
                }
            }

            // ---- Right panel: Clues + controls ----
            // gridOuter starts at X=20, is outerW wide -> right edge at 20+outerW
            // 10px gap before clue panel
            int rightX = 20 + outerW + 10;
            int rightW = Math.Max(450, screenW - rightX - 20);

            // Clue panel height: cap so it stays above the bottom bar (80px) with some margin
            int maxClueH = this.ClientSize.Height - 56 - 80 - 20;
            int clueH    = maxClueH > 200 ? maxClueH : gridViewH;

            // Clue container with gold border
            var clueOuter           = new Panel();
            clueOuter.Location      = new Point(rightX, 56);
            clueOuter.Size          = new Size(rightW + 4, clueH + 4);
            clueOuter.BackColor     = Color.Transparent;
            clueOuter.Paint        += delegate(object s, PaintEventArgs e)
            {
                using (var pen = new Pen(C_Gold, 2))
                    e.Graphics.DrawRectangle(pen, 0, 0,
                        clueOuter.Width - 1, clueOuter.Height - 1);
            };
            rootPanel.Controls.Add(clueOuter);
            clueOuter.BringToFront();

            var cluesPanel               = new Panel();
            cluesPanel.Location          = new Point(2, 2);
            cluesPanel.Size              = new Size(rightW, clueH);
            cluesPanel.AutoScroll        = true;
            cluesPanel.BackColor         = Color.FromArgb(210, 20, 10, 50);
            cluesPanels[lv]              = cluesPanel;
            clueOuter.Controls.Add(cluesPanel);
            PopulateClues(lv);

            // ---- Answer display row ----
            // Anchor to bottom of the tab so it's always visible regardless of grid height.
            // We use a bottom panel docked to the bottom of rootPanel.
            var bottomBar           = new Panel();
            bottomBar.Dock          = DockStyle.Bottom;
            bottomBar.Height        = 80;
            bottomBar.BackColor     = Color.FromArgb(220, 10, 8, 35);
            rootPanel.Controls.Add(bottomBar);
            bottomBar.BringToFront();

            // Gold top border on bottom bar
            bottomBar.Paint += delegate(object s2, PaintEventArgs e2)
            {
                using (var pen = new Pen(C_Gold, 1))
                    e2.Graphics.DrawLine(pen, 0, 0, bottomBar.Width, 0);
            };

            var ansLbl             = new Label();
            ansLbl.Text            = "Selected Answer:";
            ansLbl.AutoSize        = true;
            ansLbl.Location        = new Point(20, 8);
            ansLbl.Font            = new Font("Palatino Linotype", 11, FontStyle.Bold | FontStyle.Italic);
            ansLbl.ForeColor       = C_GoldLight;
            ansLbl.BackColor       = Color.Transparent;
            bottomBar.Controls.Add(ansLbl);

            var answerBox             = new TextBox();
            answerBox.Location        = new Point(20, 36);
            answerBox.Size            = new Size(300, 32);
            answerBox.Font            = F_Answer;
            answerBox.CharacterCasing = CharacterCasing.Upper;
            answerBox.BackColor       = Color.FromArgb(40, 20, 80);
            answerBox.ForeColor       = C_GoldLight;
            answerBox.BorderStyle     = BorderStyle.FixedSingle;
            answerBox.ReadOnly        = true;
            answerBoxes[lv]           = answerBox;
            bottomBar.Controls.Add(answerBox);

            // ---- Action buttons ----
            int lvCopy = lv;

            int btnY  = 20;
            int btnX0 = 340;
            var submitBtn = MakeCosmicButton("Submit Word",  Color.FromArgb(60, 30, 120),  Color.FromArgb(90, 50, 160),  btnX0,       btnY);
            var clearBtn  = MakeCosmicButton("Clear Word",   Color.FromArgb(100, 20, 40),  Color.FromArgb(140, 30, 60),  btnX0 + 152, btnY);
            var checkBtn  = MakeCosmicButton("Check All",    Color.FromArgb(90, 60, 10),   Color.FromArgb(130, 90, 20),  btnX0 + 304, btnY);
            var revealBtn = MakeCosmicButton("Reveal All",   Color.FromArgb(30, 60, 90),   Color.FromArgb(40, 90, 130),  btnX0 + 456, btnY);
            var resetBtn  = MakeCosmicButton("Reset",        Color.FromArgb(20, 70, 40),   Color.FromArgb(30, 100, 60),  btnX0 + 608, btnY);

            submitBtn.Click += delegate(object s, EventArgs e) { SubmitWord(lvCopy); };
            clearBtn.Click  += delegate(object s, EventArgs e) { ClearWord(lvCopy);  };
            checkBtn.Click  += delegate(object s, EventArgs e) { CheckAll(lvCopy);   };
            revealBtn.Click += delegate(object s, EventArgs e) { RevealAll(lvCopy);  };
            resetBtn.Click  += delegate(object s, EventArgs e) { ResetLevel(lvCopy); };

            bottomBar.Controls.Add(submitBtn);
            bottomBar.Controls.Add(clearBtn);
            bottomBar.Controls.Add(checkBtn);
            bottomBar.Controls.Add(revealBtn);
            bottomBar.Controls.Add(resetBtn);


        }

        // ===============================================================
        //  HELPER: make styled action button
        // ===============================================================
        private CosmicButton MakeCosmicButton(string text, Color baseColor, Color hoverColor, int x, int y)
        {
            var btn      = new CosmicButton(text, baseColor, hoverColor);
            btn.Location = new Point(x, y);
            btn.Size     = new Size(148, 40);
            return btn;
        }

        // ===============================================================
        //  POPULATE CLUES
        // ===============================================================
        private void PopulateClues(int lv)
        {
            Panel panel = cluesPanels[lv];
            panel.Controls.Clear();
            int y = 10;

            // ACROSS header
            var acrossHdr       = new Label();
            acrossHdr.Text      = "✦  ACROSS";
            acrossHdr.Font      = F_Header;
            acrossHdr.ForeColor = C_GoldLight;
            acrossHdr.BackColor = Color.Transparent;
            acrossHdr.AutoSize  = true;
            acrossHdr.Location  = new Point(10, y);
            panel.Controls.Add(acrossHdr);
            y += 24;

            foreach (WordEntry w in levels[lv].Words)
                if (w.Dir == Direction.Across)
                    y = AddClueLabel(panel, lv, w, y);

            y += 12;

            // DOWN header
            var downHdr         = new Label();
            downHdr.Text        = "✦  DOWN";
            downHdr.Font        = F_Header;
            downHdr.ForeColor   = Color.FromArgb(200, 160, 255);
            downHdr.BackColor   = Color.Transparent;
            downHdr.AutoSize    = true;
            downHdr.Location    = new Point(10, y);
            panel.Controls.Add(downHdr);
            y += 24;

            foreach (WordEntry w in levels[lv].Words)
                if (w.Dir == Direction.Down)
                    y = AddClueLabel(panel, lv, w, y);
        }

        private int AddClueLabel(Panel panel, int lv, WordEntry w, int y)
        {
            var lbl         = new Label();
            lbl.Text        = string.Format("{0}. {1}", w.Number, w.Clue);
            lbl.Font        = F_Clue;
            lbl.ForeColor   = C_TextLight;
            lbl.BackColor   = Color.Transparent;
            lbl.MaximumSize = new Size(Math.Max(300, panel.Width - 20), 400);
            lbl.AutoSize    = true;
            lbl.Location    = new Point(10, y);
            lbl.Tag         = w;
            lbl.Cursor      = Cursors.Hand;

            WordEntry wCopy  = w;
            int       lvCopy = lv;
            lbl.Click += delegate(object s, EventArgs e) { SelectWord(lvCopy, wCopy); };

            panel.Controls.Add(lbl);
            // Use actual rendered height based on panel width
            int lblH = lbl.GetPreferredSize(new Size(panel.Width - 20, 0)).Height;
            return y + lblH + 6;
        }

        // ===============================================================
        //  CELL EVENTS
        // ===============================================================

        // Helper: get all words that pass through a given cell
        private List<WordEntry> WordsAtCell(int lv, int row, int col)
        {
            var result = new List<WordEntry>();
            foreach (WordEntry w in levels[lv].Words)
                for (int i = 0; i < w.Word.Length; i++)
                {
                    int r = w.Row + (w.Dir == Direction.Down   ? i : 0);
                    int c = w.Col + (w.Dir == Direction.Across ? i : 0);
                    if (r == row && c == col) { result.Add(w); break; }
                }
            return result;
        }

        private void Cell_GotFocus(object sender, EventArgs e)
        {
            // GotFocus fires on keyboard navigation — keep current direction if possible
            TextBox tb  = (TextBox)sender;
            CellTag tag = (CellTag)tb.Tag;
            int     lv  = tag.Level;

            var words = WordsAtCell(lv, tag.Row, tag.Col);
            if (words.Count == 0) return;

            // Prefer the word matching current direction; fall back to first
            WordEntry best = words.Find(w => w.Dir.ToString().ToLower() == selDir[lv]);
            if (best == null) best = words[0];
            SelectWord(lv, best);
        }

        private void Cell_Click(object sender, EventArgs e)
        {
            TextBox tb  = (TextBox)sender;
            CellTag tag = (CellTag)tb.Tag;
            int     lv  = tag.Level;

            var words = WordsAtCell(lv, tag.Row, tag.Col);
            if (words.Count == 0) return;

            if (words.Count == 1)
            {
                // Only one word here — just select it
                SelectWord(lv, words[0]);
            }
            else
            {
                // Multiple words (Across + Down intersect): toggle direction on each click
                WordEntry current = (selWordIdx[lv] >= 0) ? levels[lv].Words[selWordIdx[lv]] : null;
                bool alreadyHere  = current != null && words.Contains(current);

                if (alreadyHere)
                {
                    // Toggle to the other word at this cell
                    int idx  = words.IndexOf(current);
                    WordEntry next = words[(idx + 1) % words.Count];
                    SelectWord(lv, next);
                }
                else
                {
                    // First click on this cell — pick direction matching current selDir, else first
                    WordEntry best = words.Find(w => w.Dir.ToString().ToLower() == selDir[lv]);
                    if (best == null) best = words[0];
                    SelectWord(lv, best);
                }
            }
        }

        private void Cell_KeyPress(object sender, KeyPressEventArgs e)
        {
            TextBox tb  = (TextBox)sender;
            CellTag tag = (CellTag)tb.Tag;
            int     lv  = tag.Level;

            if (e.KeyChar == (char)8)
            {
                tb.Text   = "";
                e.Handled = true;
                MoveToPrev(lv, tag.Row, tag.Col);
                return;
            }
            if (!char.IsLetter(e.KeyChar))
                e.Handled = true;
        }

        private void Cell_TextChanged(object sender, EventArgs e)
        {
            TextBox tb  = (TextBox)sender;
            CellTag tag = (CellTag)tb.Tag;
            int     lv  = tag.Level;

            if (tb.Text.Length == 1)
            {
                UpdateAnswerBox(lv);
                MoveToNext(lv, tag.Row, tag.Col);
            }
        }

        private void MoveToNext(int lv, int r, int c)
        {
            if (selDir[lv] == "across")
            {
                if (c + 1 < levels[lv].Cols && cells[lv, r, c + 1] != null)
                    cells[lv, r, c + 1].Focus();
            }
            else if (selDir[lv] == "down")
            {
                if (r + 1 < levels[lv].Rows && cells[lv, r + 1, c] != null)
                    cells[lv, r + 1, c].Focus();
            }
        }

        private void MoveToPrev(int lv, int r, int c)
        {
            if (selDir[lv] == "across")
            {
                if (c - 1 >= 0 && cells[lv, r, c - 1] != null)
                    cells[lv, r, c - 1].Focus();
            }
            else if (selDir[lv] == "down")
            {
                if (r - 1 >= 0 && cells[lv, r - 1, c] != null)
                    cells[lv, r - 1, c].Focus();
            }
        }

        // ===============================================================
        //  WORD SELECTION & HIGHLIGHT
        // ===============================================================
        private void SelectWord(int lv, WordEntry w)
        {
            for (int r = 0; r < levels[lv].Rows; r++)
                for (int c = 0; c < levels[lv].Cols; c++)
                    if (cells[lv, r, c] != null
                        && cells[lv, r, c].BackColor != C_Correct
                        && cells[lv, r, c].BackColor != C_Wrong)
                        cells[lv, r, c].BackColor = C_CellBg;

            selDir[lv]     = (w.Dir == Direction.Across) ? "across" : "down";
            selRow[lv]     = w.Row;
            selCol[lv]     = w.Col;
            selWordIdx[lv] = levels[lv].Words.IndexOf(w);

            for (int i = 0; i < w.Word.Length; i++)
            {
                int r = w.Row + (w.Dir == Direction.Down   ? i : 0);
                int c = w.Col + (w.Dir == Direction.Across ? i : 0);
                if (cells[lv, r, c] != null
                    && cells[lv, r, c].BackColor != C_Correct
                    && cells[lv, r, c].BackColor != C_Wrong)
                    cells[lv, r, c].BackColor = C_CellHL;
            }

            // Highlight clue label
            foreach (Control ctrl in cluesPanels[lv].Controls)
            {
                Label     lbl = ctrl as Label;
                if (lbl == null) continue;
                WordEntry we  = lbl.Tag as WordEntry;
                if (we  == null) continue;
                lbl.ForeColor = (we == w) ? C_GoldLight : C_TextLight;
                lbl.Font      = (we == w)
                    ? new Font("Palatino Linotype", 8.5f, FontStyle.Bold)
                    : F_Clue;
            }

            UpdateAnswerBox(lv);

            for (int i = 0; i < w.Word.Length; i++)
            {
                int r = w.Row + (w.Dir == Direction.Down   ? i : 0);
                int c = w.Col + (w.Dir == Direction.Across ? i : 0);
                if (cells[lv, r, c] != null && cells[lv, r, c].Text == "")
                {
                    cells[lv, r, c].Focus();
                    return;
                }
            }
            if (cells[lv, w.Row, w.Col] != null)
                cells[lv, w.Row, w.Col].Focus();
        }

        private void UpdateAnswerBox(int lv)
        {
            if (selWordIdx[lv] < 0) return;
            WordEntry w     = levels[lv].Words[selWordIdx[lv]];
            string    built = "";
            for (int i = 0; i < w.Word.Length; i++)
            {
                int r = w.Row + (w.Dir == Direction.Down   ? i : 0);
                int c = w.Col + (w.Dir == Direction.Across ? i : 0);
                built += (cells[lv, r, c] != null && cells[lv, r, c].Text != "")
                    ? cells[lv, r, c].Text : "_";
            }
            answerBoxes[lv].Text = built;
        }

        // ===============================================================
        //  GAME ACTIONS
        // ===============================================================
        private void SubmitWord(int lv)
        {
            if (selWordIdx[lv] < 0)
            {
                ShowCosmicMessage("Please select a word first.", "No Selection");
                return;
            }
            WordEntry w       = levels[lv].Words[selWordIdx[lv]];
            bool      correct = true;

            for (int i = 0; i < w.Word.Length; i++)
            {
                int     r    = w.Row + (w.Dir == Direction.Down   ? i : 0);
                int     c    = w.Col + (w.Dir == Direction.Across ? i : 0);
                TextBox cell = cells[lv, r, c];
                if (cell == null) continue;

                if (cell.Text == w.Word[i].ToString())
                    cell.BackColor = C_Correct;
                else
                {
                    cell.BackColor = C_Wrong;
                    correct        = false;
                }
            }

            if (correct)
                ShowCosmicMessage(string.Format("✦  Correct!  \"{0}\" is right!  ✦", w.Word), "Well Done!");
            else
                ShowCosmicMessage("Some letters are wrong. Keep trying!", "Incorrect");
        }

        private void ClearWord(int lv)
        {
            if (selWordIdx[lv] < 0) return;
            WordEntry w = levels[lv].Words[selWordIdx[lv]];
            for (int i = 0; i < w.Word.Length; i++)
            {
                int r = w.Row + (w.Dir == Direction.Down   ? i : 0);
                int c = w.Col + (w.Dir == Direction.Across ? i : 0);
                if (cells[lv, r, c] != null)
                {
                    cells[lv, r, c].Text      = "";
                    cells[lv, r, c].BackColor = C_CellHL;
                }
            }
            answerBoxes[lv].Text = "";
        }

        private void CheckAll(int lv)
        {
            int right = 0, wrong = 0;
            foreach (WordEntry w in levels[lv].Words)
            {
                bool wordOk = true;
                for (int i = 0; i < w.Word.Length; i++)
                {
                    int     r   = w.Row + (w.Dir == Direction.Down   ? i : 0);
                    int     c   = w.Col + (w.Dir == Direction.Across ? i : 0);
                    TextBox cel = cells[lv, r, c];
                    if (cel == null) continue;

                    if (cel.Text == w.Word[i].ToString())
                        cel.BackColor = C_Correct;
                    else
                    {
                        cel.BackColor = C_Wrong;
                        wordOk        = false;
                    }
                }
                if (wordOk) right++; else wrong++;
            }

            int total = levels[lv].Words.Count;
            if (wrong == 0)
                ShowCosmicMessage("✦  Congratulations! All words correct!  ✦", "Puzzle Complete!");
            else
                ShowCosmicMessage(
                    string.Format("Score: {0}/{1} words correct.\n{2} word(s) need attention.",
                        right, total, wrong),
                    "Check Results");
        }

        private void RevealAll(int lv)
        {
            if (MessageBox.Show("Reveal all answers?", "Reveal",
                    MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;

            foreach (WordEntry w in levels[lv].Words)
                for (int i = 0; i < w.Word.Length; i++)
                {
                    int r = w.Row + (w.Dir == Direction.Down   ? i : 0);
                    int c = w.Col + (w.Dir == Direction.Across ? i : 0);
                    if (cells[lv, r, c] != null)
                    {
                        cells[lv, r, c].Text      = w.Word[i].ToString();
                        cells[lv, r, c].BackColor = C_Correct;
                    }
                }
        }

        private void ResetLevel(int lv)
        {
            if (MessageBox.Show("Reset this level?", "Reset",
                    MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;

            for (int r = 0; r < levels[lv].Rows; r++)
                for (int c = 0; c < levels[lv].Cols; c++)
                    if (cells[lv, r, c] != null)
                    {
                        cells[lv, r, c].Text      = "";
                        cells[lv, r, c].BackColor = C_CellBg;
                    }

            answerBoxes[lv].Text = "";
            selWordIdx[lv]       = -1;
            selDir[lv]           = "";
        }

        // ---- Themed message box ----
        private void ShowCosmicMessage(string msg, string title)
        {
            MessageBox.Show(msg, title, MessageBoxButtons.OK, MessageBoxIcon.None);
        }

        // ===============================================================
        //  ENTRY POINT
        // ===============================================================
        [STAThread]
        public static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new CrosswordForm());
        }
    }
}
