# 遥感图像处理 Demo

基于 C#、Windows Forms 和 GDAL 的遥感图像处理课程项目。主程序位于 [`RSImage2026H`](RSImage2026H)，示例影像位于 [`准备/testdata`](准备/testdata)。

## 运行

1. 在 Windows 上安装 Visual Studio 与 .NET Framework 4.8 开发组件。
2. 打开 [`RSImage2026H/RSImage2026H.slnx`](RSImage2026H/RSImage2026H.slnx)，还原 `GDAL` 和 `GDAL.Native` 3.12.1 NuGet 包。
3. 生成并运行项目。程序会尝试自动加载 `准备/testdata/TMtest.tif`；也可以点击“加载图像”选择其他 GDAL 支持的影像。

功能、使用步骤和实现说明见项目目录中的两份 Word 说明书；代码结构及替代方案见 [`RSImage2026H/README.md`](RSImage2026H/README.md) 与代码说明书。

项目默认处理最长边不超过 1600 像素的预览画面。预览导出的图片不保留源影像的地理坐标及全部原始波段。
