using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using OSGeo.GDAL;

namespace RSImage2026H
{
    public partial class Form1 : Form
    {
        private string currentFile;
        private int sourceWidth;
        private int sourceHeight;
        private int bandCount;
        private Bitmap originalComposite;
        private ComboBox redChoice, greenChoice, blueChoice, singleChoice;
        private Label imageInfo;
        private NumericUpDown gammaValue, thresholdValue, clusterValue, indexValue, qualityValue;

        public Form1()
        {
            InitializeComponent();
            BuildSidebar();
            BuildTabs();
            GdalConfiguration.ConfigureGdal();
            if (!GdalConfiguration.Usable)
                labelStatus.Text = "GDAL 初始化失败，请检查程序目录中的 GDAL 文件。";
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            string sample = Path.Combine(GetSampleDirectory(), "TMtest.tif");
            if (GdalConfiguration.Usable && File.Exists(sample)) TryLoad(sample);
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            Image image = pictureBoxPreview.Image;
            pictureBoxPreview.Image = null;
            if (image != null) image.Dispose();
            if (originalComposite != null) originalComposite.Dispose();
            base.OnFormClosed(e);
        }

        private void BuildSidebar()
        {
            AddSidebarButton("📂  加载图像", OpenImage);
            AddSidebarButton("👁  显示图像", DisplayComposite);

            GroupBox group = new GroupBox { Text = "影像信息", Width = 218, Height = 96 };
            imageInfo = new Label
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(8, 6, 4, 0),
                Text = "尚未加载影像。"
            };
            group.Controls.Add(imageInfo);
            sidebar.Controls.Add(group);
            sidebar.Controls.Add(new Label { Text = "波段列表", Width = 218, Height = 24 });
            redChoice = AddBandChoice("R:");
            greenChoice = AddBandChoice("G:");
            blueChoice = AddBandChoice("B:");
            singleChoice = AddBandChoice("灰度:");

            // 与参考界面一致，列出额外波段；选择后显示对应灰度图。
            for (int number = 4; number <= 7; number++)
            {
                int defaultBand = number;
                ComboBox choice = AddBandChoice("波段 " + number + ":");
                choice.Tag = defaultBand;
                choice.SelectionChangeCommitted += (sender, args) =>
                    DisplaySingleBand(((ComboBox)sender).SelectedIndex + 1);
            }
        }

        private void AddSidebarButton(string text, Action action)
        {
            Button button = new Button { Text = text, Width = 218, Height = 38, Margin = new Padding(0, 0, 0, 5) };
            button.Click += (sender, args) => action();
            sidebar.Controls.Add(button);
        }

        private ComboBox AddBandChoice(string label)
        {
            Panel row = new Panel { Width = 218, Height = 36, Margin = new Padding(0, 0, 0, 2) };
            row.Controls.Add(new Label { Text = label, Location = new Point(0, 8), Width = 62 });
            ComboBox choice = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Location = new Point(65, 3),
                Width = 149
            };
            row.Controls.Add(choice);
            sidebar.Controls.Add(row);
            return choice;
        }

        private void BuildTabs()
        {
            FlowLayoutPanel basic = AddTab("基本操作");
            AddAction(basic, "显示单波段", () => DisplaySingleBand(singleChoice.SelectedIndex + 1));
            AddAction(basic, "恢复原图", ResetImage);
            AddAction(basic, "影像信息", ShowImageInfo);
            AddAction(basic, "保存当前预览", () => SavePreview(ImageFormat.Png, "PNG 图像|*.png", "png"));

            FlowLayoutPanel colors = AddTab("颜色及灰度处理");
            AddAction(colors, "转为灰度", () => Apply("灰度处理", ImageAlgorithms.Grayscale));
            AddAction(colors, "颜色反相", () => Apply("颜色反相", ImageAlgorithms.Invert));
            AddAction(colors, "伪彩色", () => Apply("伪彩色", ImageAlgorithms.PseudoColor));

            FlowLayoutPanel correction = AddTab("图像校正");
            AddAction(correction, "亮度 +20", () => Apply("亮度校正 +20", b => ImageAlgorithms.Brightness(b, 20)));
            AddAction(correction, "亮度 -20", () => Apply("亮度校正 -20", b => ImageAlgorithms.Brightness(b, -20)));
            gammaValue = AddNumber(correction, "Gamma", 0.2M, 3M, 0.8M, 1);
            AddAction(correction, "Gamma 校正", () => Apply("Gamma 校正", b => ImageAlgorithms.Gamma(b, (double)gammaValue.Value)));
            AddAction(correction, "顺时针旋转", () => Apply("旋转 90°", b => ImageAlgorithms.Rotate(b, RotateFlipType.Rotate90FlipNone)));
            AddAction(correction, "水平翻转", () => Apply("水平翻转", b => ImageAlgorithms.Rotate(b, RotateFlipType.RotateNoneFlipX)));
            AddAction(correction, "向右下平移", () => Apply("平移 20 像素", b => ImageAlgorithms.Translate(b, 20, 20)));

            FlowLayoutPanel transform = AddTab("图像变换");
            AddAction(transform, "NDVI (4,3)", () => ShowIndex("NDVI", 4, 3, false, 0));
            AddAction(transform, "NDWI (2,4)", () => ShowIndex("NDWI", 2, 4, false, 0));
            AddAction(transform, "单波段灰度", () => DisplaySingleBand(singleChoice.SelectedIndex + 1));

            FlowLayoutPanel enhancement = AddTab("图像增强");
            AddAction(enhancement, "直方图均衡化", () => Apply("直方图均衡化", ImageAlgorithms.Equalize));
            AddAction(enhancement, "3×3 均值平滑", () => Apply("均值平滑", ImageAlgorithms.Blur));
            AddAction(enhancement, "3×3 锐化", () => Apply("锐化", ImageAlgorithms.Sharpen));

            FlowLayoutPanel classification = AddTab("图像分类");
            clusterValue = AddNumber(classification, "类别数", 2, 8, 4, 0);
            AddAction(classification, "K 均值非监督分类", () =>
                Apply("K 均值分类", b => ImageAlgorithms.KMeans(b, (int)clusterValue.Value)));
            thresholdValue = AddNumber(classification, "阈值", 0, 255, 128, 0);
            AddAction(classification, "灰度阈值分类", () =>
                Apply("灰度阈值分类", b => ImageAlgorithms.Threshold(b, (int)thresholdValue.Value)));

            FlowLayoutPanel extraction = AddTab("信号提取");
            indexValue = AddNumber(extraction, "指数阈值", -1M, 1M, 0.2M, 2);
            AddAction(extraction, "植被掩膜 NDVI", () => ShowIndex("植被掩膜", 4, 3, true, (double)indexValue.Value));
            AddAction(extraction, "水体掩膜 NDWI", () => ShowIndex("水体掩膜", 2, 4, true, (double)indexValue.Value));
            AddAction(extraction, "Sobel 边缘", () => Apply("Sobel 边缘提取", ImageAlgorithms.Edges));

            FlowLayoutPanel compression = AddTab("图像压缩");
            qualityValue = AddNumber(compression, "JPEG 质量", 1, 100, 80, 0);
            AddAction(compression, "另存为 JPEG", SaveJpeg);
            AddAction(compression, "另存为 PNG", () => SavePreview(ImageFormat.Png, "PNG 图像|*.png", "png"));

            FlowLayoutPanel morphology = AddTab("形态学处理");
            AddAction(morphology, "二值化", () => Apply("二值化", b => ImageAlgorithms.Threshold(b, (int)thresholdValue.Value)));
            AddAction(morphology, "膨胀", () => Apply("膨胀", b => ImageAlgorithms.Morphology(b, true)));
            AddAction(morphology, "腐蚀", () => Apply("腐蚀", b => ImageAlgorithms.Morphology(b, false)));
            AddAction(morphology, "开运算", () => Apply("开运算", b =>
            {
                using (Bitmap eroded = ImageAlgorithms.Morphology(b, false))
                    return ImageAlgorithms.Morphology(eroded, true);
            }));
            AddAction(morphology, "闭运算", () => Apply("闭运算", b =>
            {
                using (Bitmap dilated = ImageAlgorithms.Morphology(b, true))
                    return ImageAlgorithms.Morphology(dilated, false);
            }));

            FlowLayoutPanel format = AddTab("格式转换");
            AddAction(format, "预览转 PNG", () => SavePreview(ImageFormat.Png, "PNG 图像|*.png", "png"));
            AddAction(format, "预览转 BMP", () => SavePreview(ImageFormat.Bmp, "BMP 图像|*.bmp", "bmp"));
            AddAction(format, "预览转 TIFF", () => SavePreview(ImageFormat.Tiff, "TIFF 图像|*.tif", "tif"));
            format.Controls.Add(new Label
            {
                Text = "预览导出不包含原始地理坐标。",
                AutoSize = true,
                Margin = new Padding(8, 14, 0, 0)
            });

            FlowLayoutPanel quality = AddTab("图像质量评价");
            AddAction(quality, "计算 MSE / PSNR / 熵", EvaluateQuality);
            AddAction(quality, "恢复原图", ResetImage);

            FlowLayoutPanel about = AddTab("关于");
            AddAction(about, "关于程序", () => MessageBox.Show(this,
                "遥感图像处理 Demo\n基于 WinForms 与 GDAL。\n处理结果为屏幕预览；原始影像不会被修改。",
                "关于", MessageBoxButtons.OK, MessageBoxIcon.Information));
        }

        private FlowLayoutPanel AddTab(string title)
        {
            TabPage page = new TabPage(title);
            FlowLayoutPanel panel = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                Padding = new Padding(8, 6, 8, 0),
                WrapContents = true
            };
            page.Controls.Add(panel);
            tabs.TabPages.Add(page);
            return panel;
        }

        private static void AddAction(FlowLayoutPanel panel, string text, Action action)
        {
            Button button = new Button { Text = text, AutoSize = true, Height = 34, Margin = new Padding(3, 3, 5, 3) };
            button.Click += (sender, args) => action();
            panel.Controls.Add(button);
        }

        private static NumericUpDown AddNumber(FlowLayoutPanel panel, string label,
            decimal minimum, decimal maximum, decimal value, int decimals)
        {
            panel.Controls.Add(new Label { Text = label, AutoSize = true, Margin = new Padding(8, 12, 1, 0) });
            NumericUpDown box = new NumericUpDown
            {
                Minimum = minimum,
                Maximum = maximum,
                Value = value,
                DecimalPlaces = decimals,
                Increment = decimals == 0 ? 1 : decimals == 1 ? 0.1M : 0.05M,
                Width = 68,
                Margin = new Padding(2, 7, 3, 0)
            };
            panel.Controls.Add(box);
            return box;
        }

        private static string GetSampleDirectory()
        {
            return Path.GetFullPath(Path.Combine(Application.StartupPath, @"..\..\..\准备\testdata"));
        }

        private void OpenImage()
        {
            using (OpenFileDialog dialog = new OpenFileDialog())
            {
                dialog.Title = "选择遥感影像";
                dialog.Filter = "遥感影像|*.tif;*.tiff;*.img;*.jp2;*.png;*.jpg;*.bmp|所有文件|*.*";
                string sampleDirectory = GetSampleDirectory();
                if (Directory.Exists(sampleDirectory)) dialog.InitialDirectory = sampleDirectory;
                if (dialog.ShowDialog(this) == DialogResult.OK) TryLoad(dialog.FileName);
            }
        }

        private void TryLoad(string path)
        {
            try
            {
                EnsureGdal();
                using (Dataset data = Gdal.Open(path, Access.GA_ReadOnly))
                {
                    if (data == null) throw new InvalidOperationException("GDAL 无法打开该影像：" + Gdal.GetLastErrorMsg());
                    if (data.RasterCount < 1) throw new InvalidOperationException("影像没有可读取的波段。");
                    int r = data.RasterCount >= 4 ? 4 : data.RasterCount >= 3 ? 3 : 1;
                    int g = data.RasterCount >= 4 ? 3 : data.RasterCount >= 3 ? 2 : 1;
                    int b = data.RasterCount >= 4 ? 2 : 1;
                    Bitmap image = RasterRenderer.Composite(data, r, g, b);
                    currentFile = path;
                    sourceWidth = data.RasterXSize;
                    sourceHeight = data.RasterYSize;
                    bandCount = data.RasterCount;
                    PopulateBands(r, g, b);
                    SetComposite(image);
                    imageInfo.Text = string.Format("图像大小：{0} × {1}\n波段数：{2}\n文件：{3}",
                        sourceWidth, sourceHeight, bandCount, Path.GetFileName(path));
                    SetStatus("已加载 " + Path.GetFileName(path));
                }
            }
            catch (Exception ex) { ShowError(ex); }
        }

        private void PopulateBands(int r, int g, int b)
        {
            List<ComboBox> choices = new List<ComboBox> { redChoice, greenChoice, blueChoice, singleChoice };
            foreach (Panel row in sidebar.Controls.OfType<Panel>())
                foreach (ComboBox choice in row.Controls.OfType<ComboBox>())
                    if (!choices.Contains(choice)) choices.Add(choice);
            foreach (ComboBox choice in choices)
            {
                choice.Items.Clear();
                for (int i = 1; i <= bandCount; i++) choice.Items.Add("Band " + i);
                int selected = choice == redChoice ? r : choice == greenChoice ? g :
                    choice == blueChoice ? b : choice == singleChoice ? Math.Min(4, bandCount) :
                    Math.Min((int)choice.Tag, bandCount);
                choice.SelectedIndex = selected - 1;
            }
        }

        private void DisplayComposite()
        {
            RunRaster("RGB 合成", data =>
                RasterRenderer.Composite(data, redChoice.SelectedIndex + 1,
                    greenChoice.SelectedIndex + 1, blueChoice.SelectedIndex + 1), true);
        }

        private void DisplaySingleBand(int band)
        {
            RunRaster("单波段灰度 Band " + band,
                data => RasterRenderer.SingleBand(data, band), false);
        }

        private void ShowIndex(string name, int firstBand, int secondBand, bool mask, double threshold)
        {
            if (bandCount < Math.Max(firstBand, secondBand))
            {
                MessageBox.Show(this, name + " 需要影像包含第 " + Math.Max(firstBand, secondBand) + " 波段。",
                    "波段不足", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            RunRaster(name, data => RasterRenderer.NormalizedDifference(data,
                firstBand, secondBand, mask, threshold), false);
        }

        private void RunRaster(string operation, Func<Dataset, Bitmap> render, bool replaceComposite)
        {
            try
            {
                EnsureSource();
                using (Dataset data = Gdal.Open(currentFile, Access.GA_ReadOnly))
                {
                    if (data == null) throw new InvalidOperationException(Gdal.GetLastErrorMsg());
                    Bitmap image = render(data);
                    if (replaceComposite) SetComposite(image);
                    else ReplaceImage(image);
                    SetStatus(operation + " 完成");
                }
            }
            catch (Exception ex) { ShowError(ex); }
        }

        private void SetComposite(Bitmap image)
        {
            Bitmap baseline = (Bitmap)image.Clone();
            Bitmap old = originalComposite;
            originalComposite = baseline;
            ReplaceImage(image);
            if (old != null) old.Dispose();
        }

        private void ReplaceImage(Bitmap image)
        {
            Image old = pictureBoxPreview.Image;
            pictureBoxPreview.Image = image;
            if (old != null) old.Dispose();
        }

        private void Apply(string operation, Func<Bitmap, Bitmap> process)
        {
            try
            {
                EnsureSource();
                Bitmap image = process((Bitmap)pictureBoxPreview.Image);
                ReplaceImage(image);
                SetStatus(operation + " 完成（当前预览）");
            }
            catch (Exception ex) { ShowError(ex); }
        }

        private void ResetImage()
        {
            try
            {
                EnsureSource();
                ReplaceImage((Bitmap)originalComposite.Clone());
                SetStatus("已恢复原始波段合成图");
            }
            catch (Exception ex) { ShowError(ex); }
        }

        private void ShowImageInfo()
        {
            try
            {
                EnsureSource();
                MessageBox.Show(this,
                    string.Format("文件：{0}\n原始尺寸：{1} × {2}\n波段数：{3}\n预览尺寸：{4} × {5}",
                        currentFile, sourceWidth, sourceHeight, bandCount,
                        pictureBoxPreview.Image.Width, pictureBoxPreview.Image.Height),
                    "影像信息", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex) { ShowError(ex); }
        }

        private void EvaluateQuality()
        {
            try
            {
                EnsureSource();
                string report = ImageAlgorithms.Quality(originalComposite, (Bitmap)pictureBoxPreview.Image);
                MessageBox.Show(this, report, "图像质量评价", MessageBoxButtons.OK, MessageBoxIcon.Information);
                SetStatus("质量评价完成");
            }
            catch (Exception ex) { ShowError(ex); }
        }

        private void SavePreview(ImageFormat format, string filter, string extension)
        {
            try
            {
                EnsureSource();
                using (SaveFileDialog dialog = new SaveFileDialog())
                {
                    dialog.Filter = filter;
                    dialog.DefaultExt = extension;
                    dialog.FileName = Path.GetFileNameWithoutExtension(currentFile) + "_preview." + extension;
                    if (dialog.ShowDialog(this) != DialogResult.OK) return;
                    EnsureDifferentFromSource(dialog.FileName);
                    pictureBoxPreview.Image.Save(dialog.FileName, format);
                    SetStatus("已保存 " + dialog.FileName);
                }
            }
            catch (Exception ex) { ShowError(ex); }
        }

        private void SaveJpeg()
        {
            try
            {
                EnsureSource();
                using (SaveFileDialog dialog = new SaveFileDialog())
                {
                    dialog.Filter = "JPEG 图像|*.jpg";
                    dialog.DefaultExt = "jpg";
                    dialog.FileName = Path.GetFileNameWithoutExtension(currentFile) + "_compressed.jpg";
                    if (dialog.ShowDialog(this) != DialogResult.OK) return;
                    EnsureDifferentFromSource(dialog.FileName);
                    ImageCodecInfo codec = ImageCodecInfo.GetImageEncoders()
                        .First(c => c.FormatID == ImageFormat.Jpeg.Guid);
                    using (EncoderParameters parameters = new EncoderParameters(1))
                    {
                        parameters.Param[0] = new EncoderParameter(Encoder.Quality, (long)qualityValue.Value);
                        pictureBoxPreview.Image.Save(dialog.FileName, codec, parameters);
                    }
                    SetStatus(string.Format("JPEG 已保存：{0}（{1:N0} 字节）",
                        dialog.FileName, new FileInfo(dialog.FileName).Length));
                }
            }
            catch (Exception ex) { ShowError(ex); }
        }

        private void EnsureGdal()
        {
            if (!GdalConfiguration.Usable)
                throw new InvalidOperationException("GDAL 初始化失败，无法读取影像。");
        }

        private void EnsureSource()
        {
            if (currentFile == null || pictureBoxPreview.Image == null)
                throw new InvalidOperationException("请先加载一幅影像。");
        }

        private void EnsureDifferentFromSource(string output)
        {
            if (string.Equals(Path.GetFullPath(output), Path.GetFullPath(currentFile),
                StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("输出文件不能覆盖当前加载的原始影像。");
        }

        private void SetStatus(string text) { labelStatus.Text = text; }

        private void ShowError(Exception ex)
        {
            labelStatus.Text = "操作失败：" + ex.Message;
            MessageBox.Show(this, ex.Message, "操作失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}
