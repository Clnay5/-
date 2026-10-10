namespace RSImage2026H
{
    partial class Form1
    {
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing && components != null) components.Dispose();
            base.Dispose(disposing);
        }

        private void InitializeComponent()
        {
            this.root = new System.Windows.Forms.TableLayoutPanel();
            this.tabs = new System.Windows.Forms.TabControl();
            this.split = new System.Windows.Forms.SplitContainer();
            this.sidebar = new System.Windows.Forms.FlowLayoutPanel();
            this.pictureBoxPreview = new System.Windows.Forms.PictureBox();
            this.labelStatus = new System.Windows.Forms.Label();
            this.root.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.split)).BeginInit();
            this.split.Panel1.SuspendLayout();
            this.split.Panel2.SuspendLayout();
            this.split.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.pictureBoxPreview)).BeginInit();
            this.SuspendLayout();
            // root
            this.root.ColumnCount = 1;
            this.root.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.root.Controls.Add(this.tabs, 0, 0);
            this.root.Controls.Add(this.split, 0, 1);
            this.root.Controls.Add(this.labelStatus, 0, 2);
            this.root.Dock = System.Windows.Forms.DockStyle.Fill;
            this.root.RowCount = 3;
            this.root.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 105F));
            this.root.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.root.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 28F));
            // tabs
            this.tabs.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tabs.Multiline = true;
            // split
            this.split.Dock = System.Windows.Forms.DockStyle.Fill;
            this.split.FixedPanel = System.Windows.Forms.FixedPanel.Panel1;
            this.split.Panel1.Controls.Add(this.sidebar);
            this.split.Panel2.Controls.Add(this.pictureBoxPreview);
            this.split.Size = new System.Drawing.Size(1200, 647);
            this.split.SplitterDistance = 245;
            // sidebar
            this.sidebar.AutoScroll = true;
            this.sidebar.Dock = System.Windows.Forms.DockStyle.Fill;
            this.sidebar.FlowDirection = System.Windows.Forms.FlowDirection.TopDown;
            this.sidebar.Padding = new System.Windows.Forms.Padding(8);
            this.sidebar.WrapContents = false;
            // pictureBoxPreview
            this.pictureBoxPreview.BackColor = System.Drawing.Color.FromArgb(230, 230, 230);
            this.pictureBoxPreview.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.pictureBoxPreview.Dock = System.Windows.Forms.DockStyle.Fill;
            this.pictureBoxPreview.SizeMode = System.Windows.Forms.PictureBoxSizeMode.Zoom;
            // labelStatus
            this.labelStatus.AutoEllipsis = true;
            this.labelStatus.BackColor = System.Drawing.Color.FromArgb(245, 245, 245);
            this.labelStatus.Dock = System.Windows.Forms.DockStyle.Fill;
            this.labelStatus.Padding = new System.Windows.Forms.Padding(10, 5, 0, 0);
            this.labelStatus.Text = "请加载 TM/GeoTIFF 影像。";
            // Form1
            this.AutoScaleDimensions = new System.Drawing.SizeF(9F, 18F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.None;
            this.ClientSize = new System.Drawing.Size(1200, 780);
            this.Controls.Add(this.root);
            this.MinimumSize = new System.Drawing.Size(900, 580);
            this.Name = "Form1";
            this.Text = "遥感图像处理-Demo";
            this.root.ResumeLayout(false);
            this.split.Panel1.ResumeLayout(false);
            this.split.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.split)).EndInit();
            this.split.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.pictureBoxPreview)).EndInit();
            this.ResumeLayout(false);
        }

        private System.Windows.Forms.TableLayoutPanel root;
        private System.Windows.Forms.TabControl tabs;
        private System.Windows.Forms.SplitContainer split;
        private System.Windows.Forms.FlowLayoutPanel sidebar;
        private System.Windows.Forms.PictureBox pictureBoxPreview;
        private System.Windows.Forms.Label labelStatus;
    }
}
