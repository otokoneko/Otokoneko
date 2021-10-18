namespace Otokoneko.Server.Gui
{
    partial class ConsoleDisplayer
    {
        /// <summary> 
        /// 必需的设计器变量。
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary> 
        /// 清理所有正在使用的资源。
        /// </summary>
        /// <param name="disposing">如果应释放托管资源，为 true；否则为 false。</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region 组件设计器生成的代码

        /// <summary> 
        /// 设计器支持所需的方法 - 不要修改
        /// 使用代码编辑器修改此方法的内容。
        /// </summary>
        private void InitializeComponent()
        {
            this.Output = new System.Windows.Forms.RichTextBox();
            this.SuspendLayout();
            // 
            // Output
            // 
            this.Output.HideSelection = false;
            this.Output.AutoWordSelection = true;
            this.Output.BackColor = System.Drawing.Color.Black;
            this.Output.Dock = System.Windows.Forms.DockStyle.Fill;
            this.Output.Font = new System.Drawing.Font("Consolas", 14F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            this.Output.ForeColor = System.Drawing.Color.Silver;
            this.Output.Location = new System.Drawing.Point(0, 0);
            this.Output.Margin = new System.Windows.Forms.Padding(0);
            this.Output.Name = "Output";
            this.Output.ScrollBars = System.Windows.Forms.RichTextBoxScrollBars.Vertical;
            this.Output.Size = new System.Drawing.Size(150, 150);
            this.Output.TabIndex = 1;
            this.Output.Text = "";
            // 
            // ConsoleDisplayer
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(14F, 31F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.Output);
            this.Margin = new System.Windows.Forms.Padding(0);
            this.Name = "ConsoleDisplayer";
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.RichTextBox Output;
    }
}
