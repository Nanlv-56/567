using Microsoft.Web.WebView2.Core;
using Newtonsoft.Json;
using System.Diagnostics;

namespace SameStatsCodeUI
{
    public partial class Form1 : Form
    {
        private Button runButton;
        private Label imageLabel;
        private Label statusLabel;
        private Label currentImageNameLabel;
        private bool monitoring = false;
        private string currentImage = "";
        private string pythonCodeFolderPath;

        public Form1()
        {
            InitializeComponent();
            this.ClientSize = new Size(1280, 720);
            tabControl1.Dock = DockStyle.Fill;
            tabPage1.Dock = DockStyle.Fill;
            tabPage2.Dock = DockStyle.Fill;
        }

        private async void tabPage1_HandleCreated(object sender, EventArgs e)
        {
            // 设置窗体大小
            webView21.Dock = DockStyle.Fill;
            await webView21.EnsureCoreWebView2Async(null);
            webView21.CoreWebView2.Navigate(Path.Combine(AppContext.BaseDirectory, "drawmydata", "index.html"));
            webView21.WebMessageReceived += webView2_WebMessageReceived;
        }

        private async void tabPage2_HandleCreated(object sender, EventArgs e)
        {
            // 获取程序的当前工作目录并构建PythonCode文件夹的路径
            string programPath = Application.StartupPath;
            pythonCodeFolderPath = Path.Combine(programPath, "PythonCode");

            // 设置窗体大小
            //tabPage2.ClientSize = new Size(800, 600);
            // 表单设置
            this.FormBorderStyle = FormBorderStyle.FixedSingle;
            this.MaximizeBox = false;
            this.Name = "SameStatsCodeUI";
            this.Text = "SameStatsCodeUI";
            this.ResumeLayout(false);

            // 添加运行按钮
            runButton = new Button
            {
                Text = "运行",
                Location = new Point(300, 10),
                Size = new Size(150, 30)
            };
            runButton.Click += RunButton_Click;
            tabPage2.Controls.Add(runButton);

            // 添加显示运行状态的Label
            this.statusLabel = new Label
            {
                Location = new Point(10, 60),
                AutoSize = true
            };
            tabPage2.Controls.Add(this.statusLabel);

            // 添加显示当前图片名称和序号的Label
            this.currentImageNameLabel = new Label
            {
                Location = new Point(10, 100),
                AutoSize = true
            };
            tabPage2.Controls.Add(this.currentImageNameLabel);

            // 添加图片显示标签
            imageLabel = new Label
            {
                Location = new Point(10, 20),
                Size = new Size(760, 400)
            };
            imageLabel.AutoSize = false;
            imageLabel.TextAlign = ContentAlignment.MiddleLeft;
            tabPage2.Controls.Add(imageLabel);

        }

        private async void webView2_WebMessageReceived(object sender, CoreWebView2WebMessageReceivedEventArgs e)
        {
            var data = e.WebMessageAsJson;
            var jsonData = JsonConvert.DeserializeObject<dynamic>(data);
            string filename = jsonData.filename;
            string content = jsonData.content;

            // 这里处理文件保存逻辑
            SaveCsvFileLocal(filename, content);
        }

        private void SaveCsvFileLocal(string filename, string content)
        {
            try
            {
                // 获取程序运行目录
                var basePath = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
                var savePath = Path.Combine(basePath, "PythonCode", "seed_datasets", filename);
                Directory.CreateDirectory(Path.GetDirectoryName(savePath));
                // 去除内容中的前缀部分并写入文件
                var actualContent = content.Replace("data:text/csv;charset=utf - 8,", "");
                File.WriteAllText(savePath, actualContent);
                MessageBox.Show("文件保存成功");
                // 自动打开文件夹
                System.Diagnostics.Process.Start("explorer.exe", Path.GetDirectoryName(savePath));
            }
            catch (Exception ex)
            {
                MessageBox.Show($"保存文件时出错: {ex.Message}");
            }
        }

        private async void RunButton_Click(object sender, EventArgs e)
        {
            this.runButton.Enabled = false;
            this.monitoring = true;
            this.currentImage = "";

            // 启动执行Python脚本的任务
            var pythonTask = Task.Run(() => ExecutePythonScript());

            // 启动监控任务（如果有相关需求，例如监控脚本执行结果相关的文件等）
            var monitorTask = Task.Run(() =>
            {
                Thread.Sleep(500);
                // 启动监控任务，延迟500毫秒后开始
                MonitorResults();
            });

            await Task.WhenAll(pythonTask, monitorTask);

            this.runButton.Enabled = true;
            this.monitoring = false;
        }

        private void ExecutePythonScript()
        {
            // 在执行Python脚本之前清空results文件夹
            string resultsFolderPath = Path.Combine(pythonCodeFolderPath, "results");
            if (Directory.Exists(resultsFolderPath))
            {
                Directory.Delete(resultsFolderPath, true);
                Directory.CreateDirectory(resultsFolderPath);
            }

            ProcessStartInfo startInfo = new ProcessStartInfo();
            startInfo.FileName = "python";
            startInfo.Arguments = "same_stats.py run rando circle 200000 2 150";
            startInfo.RedirectStandardOutput = true;
            startInfo.RedirectStandardError = true;
            startInfo.UseShellExecute = false;
            startInfo.CreateNoWindow = true;
            startInfo.WorkingDirectory = pythonCodeFolderPath;

            // 使用Invoke确保在UI线程中更新Label
            this.Invoke((MethodInvoker)(() =>
            {
                statusLabel.Text = "开始执行Python脚本";
            }));

            Console.WriteLine("开始执行Python脚本");

            try
            {
                using (Process process = new Process { StartInfo = startInfo })
                {
                    process.OutputDataReceived += (sender, e) =>
                    {
                        if (!string.IsNullOrEmpty(e.Data))
                        {
                            Console.WriteLine("输出: " + e.Data);
                            this.Invoke((MethodInvoker)(() =>
                            {

                                statusLabel.Text = "输出: " + e.Data;
                            }));
                        }
                    };

                    process.ErrorDataReceived += (sender, e) =>
                    {
                        if (!string.IsNullOrEmpty(e.Data))
                        {
                            Console.WriteLine("错误: " + e.Data);
                            this.Invoke((MethodInvoker)(() =>
                            {
                                statusLabel.Text = "错误: " + e.Data;
                            }));
                        }
                    };

                    process.Start();

                    // 开始异步读取输出
                    process.BeginOutputReadLine();
                    process.BeginErrorReadLine();

                    process.WaitForExit();

                    Console.WriteLine("Python脚本执行结束");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"进程启动失败: {ex.Message}");
            }
        }

        private void MonitorResults()
        {
            string resultsFolderPath = Path.Combine(pythonCodeFolderPath, "results");
            FileSystemWatcher watcher = new FileSystemWatcher
            {
                Path = resultsFolderPath,
                Filter = "circle-image-*.png",
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite,
                EnableRaisingEvents = true
            };

            watcher.Created += (s, e) =>
            {
                // 等待文件完全写入（防止文件锁问题）
                Thread.Sleep(500);
                if (e.FullPath != this.currentImage)
                {
                    this.currentImage = e.FullPath;
                    this.Invoke((MethodInvoker)(() =>
                    {
                        ShowImage(e.FullPath);
                    }));
                }
            };

            while (this.monitoring)
            {
                Thread.Sleep(100); // 保持线程运行
            }

            watcher.EnableRaisingEvents = false;
        }


        private void ShowImage(string imagePath)
        {
            using (Bitmap bitmap = new Bitmap(imagePath))
            {
                // 获取图片原始尺寸
                int imgWidth = bitmap.Width;
                int imgHeight = bitmap.Height;

                // 计算缩放比例，以Label的宽度为基础保持等比例
                double scale = (double)this.imageLabel.Width / imgWidth;
                int newWidth = this.imageLabel.Width;
                int newHeight = (int)(imgHeight * scale);

                // 如果计算出的新高度超过Label的高度，则重新计算以Label高度为基础
                if (newHeight > this.imageLabel.Height)
                {
                    scale = (double)this.imageLabel.Height / imgHeight;
                    newWidth = (int)(imgWidth * scale);
                    newHeight = this.imageLabel.Height;
                }

                Image scaledImage = new Bitmap(bitmap, new Size(newWidth, newHeight));
                this.imageLabel.Image = scaledImage;

                // 获取图片名称和序号
                string fileName = Path.GetFileName(imagePath);
                int index = int.Parse(Path.GetFileNameWithoutExtension(fileName).Split('-')[2]);
                this.currentImageNameLabel.Text = $"当前图片: {fileName} (序号: {index})";
            }
        }
    }
}