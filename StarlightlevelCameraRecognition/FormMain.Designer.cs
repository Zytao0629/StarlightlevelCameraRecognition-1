namespace StarlightlevelCameraRecognition
{
    partial class FormMain
    {
        /// <summary>
        ///  Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        ///  Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        ///  Required method for Designer support - do not modify
        ///  the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            pictureBox1 = new PictureBox();
            button1 = new Button();
            statusStrip1 = new StatusStrip();
            toolStripStatusLabel1 = new ToolStripStatusLabel();
            toolStripStatusLabel2 = new ToolStripStatusLabel();
            toolStripStatusLabel3 = new ToolStripStatusLabel();
            toolStripStatusLabel4 = new ToolStripStatusLabel();
            button2 = new Button();
            richTextBox1 = new RichTextBox();
            groupBox1 = new GroupBox();
            button3 = new Button();
            textBox1 = new TextBox();
            button4 = new Button();
            button5 = new Button();
            groupBox2 = new GroupBox();
            textBoxEndAngle = new TextBox();
            label10 = new Label();
            textBoxStartAngle = new TextBox();
            label9 = new Label();
            numericUpDownExerciseDuration = new NumericUpDown();
            label8 = new Label();
            label7 = new Label();
            numericUpDownCleanTime = new NumericUpDown();
            numericUpDownOverlapThreshold = new NumericUpDown();
            numericUpDownRegionMinSize = new NumericUpDown();
            numericUpDownDiffThreshold = new NumericUpDown();
            numericUpDownHighlightDuration = new NumericUpDown();
            numericUpDownBrushSize = new NumericUpDown();
            label5 = new Label();
            label4 = new Label();
            label3 = new Label();
            label2 = new Label();
            label1 = new Label();
            comboBoxPorts = new ComboBox();
            label6 = new Label();
            groupBox3 = new GroupBox();
            button6 = new Button();
            textBox2 = new TextBox();
            button8 = new Button();
            ((System.ComponentModel.ISupportInitialize)pictureBox1).BeginInit();
            statusStrip1.SuspendLayout();
            groupBox1.SuspendLayout();
            groupBox2.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)numericUpDownExerciseDuration).BeginInit();
            ((System.ComponentModel.ISupportInitialize)numericUpDownCleanTime).BeginInit();
            ((System.ComponentModel.ISupportInitialize)numericUpDownOverlapThreshold).BeginInit();
            ((System.ComponentModel.ISupportInitialize)numericUpDownRegionMinSize).BeginInit();
            ((System.ComponentModel.ISupportInitialize)numericUpDownDiffThreshold).BeginInit();
            ((System.ComponentModel.ISupportInitialize)numericUpDownHighlightDuration).BeginInit();
            ((System.ComponentModel.ISupportInitialize)numericUpDownBrushSize).BeginInit();
            groupBox3.SuspendLayout();
            SuspendLayout();
            // 
            // pictureBox1
            // 
            pictureBox1.BackColor = SystemColors.ActiveCaption;
            pictureBox1.Location = new Point(12, 35);
            pictureBox1.Name = "pictureBox1";
            pictureBox1.Size = new Size(640, 480);
            pictureBox1.TabIndex = 0;
            pictureBox1.TabStop = false;
            // 
            // button1
            // 
            button1.Location = new Point(658, 12);
            button1.Name = "button1";
            button1.Size = new Size(149, 84);
            button1.TabIndex = 1;
            button1.Text = "打开相机";
            button1.UseVisualStyleBackColor = true;
            // 
            // statusStrip1
            // 
            statusStrip1.ImageScalingSize = new Size(24, 24);
            statusStrip1.Items.AddRange(new ToolStripItem[] { toolStripStatusLabel1, toolStripStatusLabel2, toolStripStatusLabel3, toolStripStatusLabel4 });
            statusStrip1.Location = new Point(0, 835);
            statusStrip1.Name = "statusStrip1";
            statusStrip1.Size = new Size(1632, 31);
            statusStrip1.TabIndex = 2;
            statusStrip1.Text = "statusStrip1";
            // 
            // toolStripStatusLabel1
            // 
            toolStripStatusLabel1.ForeColor = Color.Black;
            toolStripStatusLabel1.Name = "toolStripStatusLabel1";
            toolStripStatusLabel1.Size = new Size(60, 24);
            toolStripStatusLabel1.Text = "          ";
            // 
            // toolStripStatusLabel2
            // 
            toolStripStatusLabel2.ForeColor = Color.Black;
            toolStripStatusLabel2.Name = "toolStripStatusLabel2";
            toolStripStatusLabel2.Size = new Size(110, 24);
            toolStripStatusLabel2.Text = "                    ";
            // 
            // toolStripStatusLabel3
            // 
            toolStripStatusLabel3.ForeColor = Color.Black;
            toolStripStatusLabel3.Name = "toolStripStatusLabel3";
            toolStripStatusLabel3.Size = new Size(120, 24);
            toolStripStatusLabel3.Text = "                      ";
            // 
            // toolStripStatusLabel4
            // 
            toolStripStatusLabel4.Name = "toolStripStatusLabel4";
            toolStripStatusLabel4.Size = new Size(235, 24);
            toolStripStatusLabel4.Text = "                                             ";
            // 
            // button2
            // 
            button2.Location = new Point(658, 205);
            button2.Name = "button2";
            button2.Size = new Size(149, 85);
            button2.TabIndex = 3;
            button2.Text = "关闭相机";
            button2.UseVisualStyleBackColor = true;
            // 
            // richTextBox1
            // 
            richTextBox1.BackColor = Color.FromArgb(255, 255, 192);
            richTextBox1.Location = new Point(673, 459);
            richTextBox1.Name = "richTextBox1";
            richTextBox1.Size = new Size(609, 373);
            richTextBox1.TabIndex = 4;
            richTextBox1.Text = "";
            // 
            // groupBox1
            // 
            groupBox1.Controls.Add(button3);
            groupBox1.Controls.Add(textBox1);
            groupBox1.Location = new Point(1287, 486);
            groupBox1.Name = "groupBox1";
            groupBox1.Size = new Size(345, 187);
            groupBox1.TabIndex = 5;
            groupBox1.TabStop = false;
            groupBox1.Text = "手动发送命令";
            // 
            // button3
            // 
            button3.Location = new Point(81, 78);
            button3.Name = "button3";
            button3.Size = new Size(156, 61);
            button3.TabIndex = 1;
            button3.Text = "发送命令";
            button3.UseVisualStyleBackColor = true;
            // 
            // textBox1
            // 
            textBox1.Location = new Point(40, 42);
            textBox1.Name = "textBox1";
            textBox1.Size = new Size(249, 30);
            textBox1.TabIndex = 0;
            // 
            // button4
            // 
            button4.Location = new Point(658, 300);
            button4.Name = "button4";
            button4.Size = new Size(149, 75);
            button4.TabIndex = 6;
            button4.Text = "捕获图像";
            button4.UseVisualStyleBackColor = true;
            // 
            // button5
            // 
            button5.Location = new Point(658, 381);
            button5.Name = "button5";
            button5.Size = new Size(149, 75);
            button5.TabIndex = 7;
            button5.Text = "开启监测";
            button5.UseVisualStyleBackColor = true;
            // 
            // groupBox2
            // 
            groupBox2.Controls.Add(textBoxEndAngle);
            groupBox2.Controls.Add(label10);
            groupBox2.Controls.Add(textBoxStartAngle);
            groupBox2.Controls.Add(label9);
            groupBox2.Controls.Add(numericUpDownExerciseDuration);
            groupBox2.Controls.Add(label8);
            groupBox2.Controls.Add(label7);
            groupBox2.Controls.Add(numericUpDownCleanTime);
            groupBox2.Controls.Add(numericUpDownOverlapThreshold);
            groupBox2.Controls.Add(numericUpDownRegionMinSize);
            groupBox2.Controls.Add(numericUpDownDiffThreshold);
            groupBox2.Controls.Add(numericUpDownHighlightDuration);
            groupBox2.Controls.Add(numericUpDownBrushSize);
            groupBox2.Controls.Add(label5);
            groupBox2.Controls.Add(label4);
            groupBox2.Controls.Add(label3);
            groupBox2.Controls.Add(label2);
            groupBox2.Controls.Add(label1);
            groupBox2.Location = new Point(813, 35);
            groupBox2.Name = "groupBox2";
            groupBox2.Size = new Size(413, 421);
            groupBox2.TabIndex = 8;
            groupBox2.TabStop = false;
            groupBox2.Text = "变值控制";
            // 
            // textBoxEndAngle
            // 
            textBoxEndAngle.Location = new Point(172, 329);
            textBoxEndAngle.Name = "textBoxEndAngle";
            textBoxEndAngle.Size = new Size(124, 30);
            textBoxEndAngle.TabIndex = 22;
            // 
            // label10
            // 
            label10.AutoSize = true;
            label10.Location = new Point(148, 329);
            label10.Name = "label10";
            label10.Size = new Size(18, 24);
            label10.TabIndex = 21;
            label10.Text = "-";
            // 
            // textBoxStartAngle
            // 
            textBoxStartAngle.Location = new Point(18, 329);
            textBoxStartAngle.Name = "textBoxStartAngle";
            textBoxStartAngle.Size = new Size(124, 30);
            textBoxStartAngle.TabIndex = 20;
            // 
            // label9
            // 
            label9.AutoSize = true;
            label9.Location = new Point(6, 298);
            label9.Name = "label9";
            label9.Size = new Size(136, 24);
            label9.TabIndex = 19;
            label9.Text = "舵机运动角度：";
            // 
            // numericUpDownExerciseDuration
            // 
            numericUpDownExerciseDuration.Location = new Point(142, 265);
            numericUpDownExerciseDuration.Name = "numericUpDownExerciseDuration";
            numericUpDownExerciseDuration.Size = new Size(180, 30);
            numericUpDownExerciseDuration.TabIndex = 18;
            numericUpDownExerciseDuration.Value = new decimal(new int[] { 10, 0, 0, 0 });
            // 
            // label8
            // 
            label8.AutoSize = true;
            label8.Location = new Point(6, 267);
            label8.Name = "label8";
            label8.Size = new Size(136, 24);
            label8.TabIndex = 17;
            label8.Text = "舵机运动时长：";
            // 
            // label7
            // 
            label7.AutoSize = true;
            label7.Location = new Point(6, 233);
            label7.Name = "label7";
            label7.Size = new Size(136, 24);
            label7.TabIndex = 16;
            label7.Text = "底图更换周期：";
            // 
            // numericUpDownCleanTime
            // 
            numericUpDownCleanTime.Location = new Point(142, 231);
            numericUpDownCleanTime.Name = "numericUpDownCleanTime";
            numericUpDownCleanTime.Size = new Size(180, 30);
            numericUpDownCleanTime.TabIndex = 15;
            numericUpDownCleanTime.Value = new decimal(new int[] { 5, 0, 0, 0 });
            // 
            // numericUpDownOverlapThreshold
            // 
            numericUpDownOverlapThreshold.DecimalPlaces = 1;
            numericUpDownOverlapThreshold.Increment = new decimal(new int[] { 1, 0, 0, 65536 });
            numericUpDownOverlapThreshold.Location = new Point(142, 195);
            numericUpDownOverlapThreshold.Maximum = new decimal(new int[] { 1, 0, 0, 0 });
            numericUpDownOverlapThreshold.Name = "numericUpDownOverlapThreshold";
            numericUpDownOverlapThreshold.Size = new Size(180, 30);
            numericUpDownOverlapThreshold.TabIndex = 9;
            numericUpDownOverlapThreshold.Value = new decimal(new int[] { 3, 0, 0, 65536 });
            numericUpDownOverlapThreshold.ValueChanged += numericUpDownOverlapThreshold_ValueChanged;
            // 
            // numericUpDownRegionMinSize
            // 
            numericUpDownRegionMinSize.Increment = new decimal(new int[] { 10, 0, 0, 0 });
            numericUpDownRegionMinSize.Location = new Point(142, 159);
            numericUpDownRegionMinSize.Maximum = new decimal(new int[] { 300, 0, 0, 0 });
            numericUpDownRegionMinSize.Name = "numericUpDownRegionMinSize";
            numericUpDownRegionMinSize.Size = new Size(180, 30);
            numericUpDownRegionMinSize.TabIndex = 8;
            numericUpDownRegionMinSize.Value = new decimal(new int[] { 30, 0, 0, 0 });
            numericUpDownRegionMinSize.ValueChanged += numericUpDownRegionMinSize_ValueChanged;
            // 
            // numericUpDownDiffThreshold
            // 
            numericUpDownDiffThreshold.Increment = new decimal(new int[] { 10, 0, 0, 0 });
            numericUpDownDiffThreshold.Location = new Point(142, 122);
            numericUpDownDiffThreshold.Maximum = new decimal(new int[] { 255, 0, 0, 0 });
            numericUpDownDiffThreshold.Name = "numericUpDownDiffThreshold";
            numericUpDownDiffThreshold.Size = new Size(180, 30);
            numericUpDownDiffThreshold.TabIndex = 7;
            numericUpDownDiffThreshold.Value = new decimal(new int[] { 70, 0, 0, 0 });
            numericUpDownDiffThreshold.ValueChanged += numericUpDownDiffThreshold_ValueChanged;
            // 
            // numericUpDownHighlightDuration
            // 
            numericUpDownHighlightDuration.Increment = new decimal(new int[] { 10, 0, 0, 0 });
            numericUpDownHighlightDuration.Location = new Point(142, 86);
            numericUpDownHighlightDuration.Maximum = new decimal(new int[] { 200, 0, 0, 0 });
            numericUpDownHighlightDuration.Name = "numericUpDownHighlightDuration";
            numericUpDownHighlightDuration.Size = new Size(180, 30);
            numericUpDownHighlightDuration.TabIndex = 6;
            numericUpDownHighlightDuration.Value = new decimal(new int[] { 20, 0, 0, 0 });
            numericUpDownHighlightDuration.ValueChanged += numericUpDownHighlightDuration_ValueChanged;
            // 
            // numericUpDownBrushSize
            // 
            numericUpDownBrushSize.Increment = new decimal(new int[] { 10, 0, 0, 0 });
            numericUpDownBrushSize.Location = new Point(142, 47);
            numericUpDownBrushSize.Maximum = new decimal(new int[] { 200, 0, 0, 0 });
            numericUpDownBrushSize.Name = "numericUpDownBrushSize";
            numericUpDownBrushSize.Size = new Size(180, 30);
            numericUpDownBrushSize.TabIndex = 5;
            numericUpDownBrushSize.Value = new decimal(new int[] { 100, 0, 0, 0 });
            numericUpDownBrushSize.ValueChanged += numericUpDownBrushSize_ValueChanged;
            // 
            // label5
            // 
            label5.AutoSize = true;
            label5.Location = new Point(36, 201);
            label5.Name = "label5";
            label5.Size = new Size(100, 24);
            label5.TabIndex = 4;
            label5.Text = "重叠比例：";
            // 
            // label4
            // 
            label4.AutoSize = true;
            label4.Location = new Point(6, 161);
            label4.Name = "label4";
            label4.Size = new Size(136, 24);
            label4.TabIndex = 3;
            label4.Text = "异物最小尺寸：";
            // 
            // label3
            // 
            label3.AutoSize = true;
            label3.Location = new Point(18, 124);
            label3.Name = "label3";
            label3.Size = new Size(118, 24);
            label3.TabIndex = 2;
            label3.Text = "亮度差阈值：";
            // 
            // label2
            // 
            label2.AutoSize = true;
            label2.Location = new Point(36, 86);
            label2.Name = "label2";
            label2.Size = new Size(100, 24);
            label2.TabIndex = 1;
            label2.Text = "高亮时间：";
            // 
            // label1
            // 
            label1.AutoSize = true;
            label1.Location = new Point(36, 53);
            label1.Name = "label1";
            label1.Size = new Size(100, 24);
            label1.TabIndex = 0;
            label1.Text = "画笔大小：";
            // 
            // comboBoxPorts
            // 
            comboBoxPorts.FormattingEnabled = true;
            comboBoxPorts.Location = new Point(1384, 64);
            comboBoxPorts.Name = "comboBoxPorts";
            comboBoxPorts.Size = new Size(192, 32);
            comboBoxPorts.TabIndex = 9;
            // 
            // label6
            // 
            label6.AutoSize = true;
            label6.Location = new Point(1340, 35);
            label6.Name = "label6";
            label6.Size = new Size(163, 24);
            label6.TabIndex = 10;
            label6.Text = "通讯COM口选择：";
            // 
            // groupBox3
            // 
            groupBox3.Controls.Add(button6);
            groupBox3.Controls.Add(textBox2);
            groupBox3.Location = new Point(1232, 208);
            groupBox3.Name = "groupBox3";
            groupBox3.Size = new Size(291, 222);
            groupBox3.TabIndex = 11;
            groupBox3.TabStop = false;
            groupBox3.Text = "命令补齐";
            // 
            // button6
            // 
            button6.Location = new Point(60, 125);
            button6.Name = "button6";
            button6.Size = new Size(156, 61);
            button6.TabIndex = 2;
            button6.Text = "计算CRC";
            button6.UseVisualStyleBackColor = true;
            // 
            // textBox2
            // 
            textBox2.Location = new Point(22, 66);
            textBox2.Name = "textBox2";
            textBox2.Size = new Size(249, 30);
            textBox2.TabIndex = 1;
            // 
            // button8
            // 
            button8.Location = new Point(658, 103);
            button8.Name = "button8";
            button8.Size = new Size(149, 84);
            button8.TabIndex = 13;
            button8.Text = "保存涂抹层";
            button8.UseVisualStyleBackColor = true;
            // 
            // FormMain
            // 
            AutoScaleDimensions = new SizeF(11F, 24F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(1632, 866);
            Controls.Add(button8);
            Controls.Add(groupBox3);
            Controls.Add(label6);
            Controls.Add(comboBoxPorts);
            Controls.Add(groupBox2);
            Controls.Add(button5);
            Controls.Add(button4);
            Controls.Add(groupBox1);
            Controls.Add(richTextBox1);
            Controls.Add(button2);
            Controls.Add(statusStrip1);
            Controls.Add(button1);
            Controls.Add(pictureBox1);
            Name = "FormMain";
            Text = "Form1";
            ((System.ComponentModel.ISupportInitialize)pictureBox1).EndInit();
            statusStrip1.ResumeLayout(false);
            statusStrip1.PerformLayout();
            groupBox1.ResumeLayout(false);
            groupBox1.PerformLayout();
            groupBox2.ResumeLayout(false);
            groupBox2.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)numericUpDownExerciseDuration).EndInit();
            ((System.ComponentModel.ISupportInitialize)numericUpDownCleanTime).EndInit();
            ((System.ComponentModel.ISupportInitialize)numericUpDownOverlapThreshold).EndInit();
            ((System.ComponentModel.ISupportInitialize)numericUpDownRegionMinSize).EndInit();
            ((System.ComponentModel.ISupportInitialize)numericUpDownDiffThreshold).EndInit();
            ((System.ComponentModel.ISupportInitialize)numericUpDownHighlightDuration).EndInit();
            ((System.ComponentModel.ISupportInitialize)numericUpDownBrushSize).EndInit();
            groupBox3.ResumeLayout(false);
            groupBox3.PerformLayout();
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private PictureBox pictureBox1;
        private Button button1;
        private StatusStrip statusStrip1;
        private ToolStripStatusLabel toolStripStatusLabel1;
        private ToolStripStatusLabel toolStripStatusLabel2;
        private ToolStripStatusLabel toolStripStatusLabel3;
        private Button button2;
        private RichTextBox richTextBox1;
        private GroupBox groupBox1;
        private Button button3;
        private TextBox textBox1;
        private Button button4;
        private ToolStripStatusLabel toolStripStatusLabel4;
        private Button button5;
        private GroupBox groupBox2;
        private Label label2;
        private Label label1;
        private Label label3;
        private Label label5;
        private Label label4;
        private NumericUpDown numericUpDownOverlapThreshold;
        private NumericUpDown numericUpDownRegionMinSize;
        private NumericUpDown numericUpDownDiffThreshold;
        private NumericUpDown numericUpDownHighlightDuration;
        private NumericUpDown numericUpDownBrushSize;
        private ComboBox comboBoxPorts;
        private Label label6;
        private GroupBox groupBox3;
        private Button button6;
        private TextBox textBox2;
        private Button button8;
        private NumericUpDown numericUpDownCleanTime;
        private Label label7;
        private Label label8;
        private NumericUpDown numericUpDownExerciseDuration;
        private Label label9;
        private TextBox textBoxEndAngle;
        private Label label10;
        private TextBox textBoxStartAngle;
    }
}
