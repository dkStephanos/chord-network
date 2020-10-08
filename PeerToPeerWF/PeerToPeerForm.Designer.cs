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
         this.txtMain.Enabled = false;
         this.txtMain.Location = new System.Drawing.Point(12, 12);
         this.txtMain.MaximumSize = new System.Drawing.Size(776, 378);
         this.txtMain.MinimumSize = new System.Drawing.Size(776, 378);
         this.txtMain.Name = "txtMain";
         this.txtMain.ScrollBars = System.Windows.Forms.RichTextBoxScrollBars.Vertical;
         this.txtMain.Size = new System.Drawing.Size(776, 378);
         this.txtMain.TabIndex = 0;
         this.txtMain.Text = "";
         // 
         // label1
         // 
         this.label1.AutoSize = true;
         this.label1.Location = new System.Drawing.Point(12, 411);
         this.label1.Name = "label1";
         this.label1.Size = new System.Drawing.Size(42, 15);
         this.label1.TabIndex = 1;
         this.label1.Text = "CMD>";
         // 
         // commandBox
         // 
         this.commandBox.Location = new System.Drawing.Point(56, 407);
         this.commandBox.Name = "commandBox";
         this.commandBox.Size = new System.Drawing.Size(651, 23);
         this.commandBox.TabIndex = 2;
         // 
         // btnSend
         // 
         this.btnSend.Location = new System.Drawing.Point(713, 407);
         this.btnSend.Name = "btnSend";
         this.btnSend.Size = new System.Drawing.Size(75, 23);
         this.btnSend.TabIndex = 3;
         this.btnSend.Text = "Send";
         this.btnSend.UseVisualStyleBackColor = true;
         this.btnSend.MouseClick += new System.Windows.Forms.MouseEventHandler(this.btnSend_MouseClick);
         // 
         // PeerToPeerForm
         // 
         this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
         this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
         this.ClientSize = new System.Drawing.Size(800, 450);
         this.Controls.Add(this.btnSend);
         this.Controls.Add(this.commandBox);
         this.Controls.Add(this.label1);
         this.Controls.Add(this.txtMain);
         this.MaximizeBox = false;
         this.Name = "PeerToPeerForm";
         this.Text = "Peer to Peer App";
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

