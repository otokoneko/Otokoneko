namespace Otokoneko.Server.Gui
{
    partial class OtokonekoServer
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
            this.Output = new ConsoleDisplayer();
            this.SuspendLayout();
            // 
            // Output
            // 
            this.Output.Dock = System.Windows.Forms.DockStyle.Fill;
            this.Output.Location = new System.Drawing.Point(0, 0);
            this.Output.Margin = new System.Windows.Forms.Padding(0);
            this.Output.Name = "Output";
            this.Output.TabIndex = 0;
            // 
            // OtokonekoServer
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(14F, 31F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1574, 829);
            this.Controls.Add(this.Output);
            this.ForeColor = System.Drawing.SystemColors.ControlText;
            this.Name = "OtokonekoServer";
            this.Text = "OtokonekoServer";
            this.ResumeLayout(false);

        }

        #endregion

        private ConsoleDisplayer Output;
    }
}

