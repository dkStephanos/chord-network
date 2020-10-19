namespace PeerToPeerWF
{
   partial class PeerToPeerForm
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
         this.txtMain = new System.Windows.Forms.RichTextBox();
         this.label1 = new System.Windows.Forms.Label();
         this.commandBox = new System.Windows.Forms.TextBox();
         this.btnSend = new System.Windows.Forms.Button();
         this.SuspendLayout();
         // 
         // txtMain
         // 
         this.txtMain.BackColor = System.Drawing.Color.Silver;
         this.txtMain.ForeColor = System.Drawing.Color.Black;
         this.txtMain.Location = new System.Drawing.Point(12, 14);
         this.txtMain.MaximumSize = new System.Drawing.Size(776, 428);
         this.txtMain.MinimumSize = new System.Drawing.Size(776, 428);
         this.txtMain.Name = "txtMain";
         this.txtMain.ReadOnly = true;
         this.txtMain.ScrollBars = System.Windows.Forms.RichTextBoxScrollBars.Vertical;
         this.txtMain.Size = new System.Drawing.Size(776, 428);
         this.txtMain.TabIndex = 0;
         this.txtMain.Text = "";
         // 
         // label1
         // 
         this.label1.AutoSize = true;
         this.label1.Location = new System.Drawing.Point(12, 466);
         this.label1.Name = "label1";
         this.label1.Size = new System.Drawing.Size(46, 17);
         this.label1.TabIndex = 1;
         this.label1.Text = "CMD>";
         // 
         // commandBox
         // 
         this.commandBox.BackColor = System.Drawing.Color.Gray;
         this.commandBox.Location = new System.Drawing.Point(56, 461);
         this.commandBox.Name = "commandBox";
         this.commandBox.Size = new System.Drawing.Size(651, 25);
         this.commandBox.TabIndex = 1;
         this.commandBox.KeyDown += new System.Windows.Forms.KeyEventHandler(this.commandBox_KeyDown);
         // 
         // btnSend
         // 
         this.btnSend.BackColor = System.Drawing.Color.Gray;
         this.btnSend.Location = new System.Drawing.Point(713, 461);
         this.btnSend.Name = "btnSend";
         this.btnSend.Size = new System.Drawing.Size(75, 26);
         this.btnSend.TabIndex = 3;
         this.btnSend.Text = "Send";
         this.btnSend.UseVisualStyleBackColor = false;
         this.btnSend.MouseClick += new System.Windows.Forms.MouseEventHandler(this.btnSend_MouseClick);
         // 
         // PeerToPeerForm
         // 
         this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 17F);
         this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
         this.AutoScroll = true;
         this.BackColor = System.Drawing.Color.Black;
         this.ClientSize = new System.Drawing.Size(800, 510);
         this.Controls.Add(this.btnSend);
         this.Controls.Add(this.commandBox);
         this.Controls.Add(this.label1);
         this.Controls.Add(this.txtMain);
         this.ForeColor = System.Drawing.Color.WhiteSmoke;
         this.MaximizeBox = false;
         this.Name = "PeerToPeerForm";
         this.Text = "Chord Node";
         this.Load += new System.EventHandler(this.PeerToPeerForm_Load);
         this.ResumeLayout(false);
         this.PerformLayout();

      }

      #endregion

      private System.Windows.Forms.RichTextBox txtMain;
      private System.Windows.Forms.Label label1;
      private System.Windows.Forms.TextBox commandBox;
      private System.Windows.Forms.Button btnSend;
   }
}

