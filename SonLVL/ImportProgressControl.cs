using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using SonicRetro.SonLVL.API;

namespace SonicRetro.SonLVL
{
	public partial class ImportProgressControl : UserControl
	{
		public ImportProgressControl()
		{
			InitializeComponent();
		}

		int delayframes = 0;
		int curframe = 0;
		Point offset;
		List<Sprite> sprites;
		Animation animation;
		Color[] palette;

		/*public void ChangeAnimation(byte[] art, MappingsFrame[] mappings, DPLCFrame[] dplcs, Animation animation, Color[] palette)
		{
			sprites = new List<Sprite>(mappings.Length);
			for (int i = 0; i < mappings.Length; i++)
			{
				if (dplcs == null)
					sprites.Add(new Sprite(LevelData.MapFrameToBmp(art, mappings[i], 0)));
				else
					sprites.Add(new Sprite(LevelData.MapFrameToBmp(art, mappings[i], dplcs[i], 0)));
			}
			this.animation = animation;
			this.palette = palette;
		}*/

		public void ChangeAnimation(byte[] art, List<SpriteInfo[]> sprites, Animation animation, Color[] palette)
		{
			this.sprites = new List<Sprite>();
			for (int i = 0; i < sprites.Count; i++)
				this.sprites.Add(ObjectDefinition.LoadSpriteData(art, 0x00, -1, -2, sprites[i]));
			this.animation = animation;
			this.palette = palette;
		}

		void anitimer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
		{
			if (delayframes++ >= animation.Speed[curframe])
			{
				if (curframe >= animation.Count - 1)
				{
					switch (animation.EndType)
					{
						case 0xFF:
						case 0xFB:
							curframe = 0;
							break;
						case 0xFE:
							curframe = animation.Count - animation.ExtraParam;
							break;
						default:
							curframe = 0;
							break;
					}
				}
				else
					curframe++;
				delayframes = 0;
				DrawPreview();
			}
		}

		private void ImportProgressControl_VisibleChanged(object sender, EventArgs e)
		{
			if (DesignMode) return;
			if (Visible)
			{
				int left = 0;
				int right = 0;
				int top = 0;
				int bottom = 0;
				foreach (byte b in animation.Frames)
				{
					Sprite spr = sprites[b];
					if (spr.Image != null)
					{
						left = Math.Min(spr.Left, left);
						right = Math.Max(spr.Right, right);
						top = Math.Min(spr.Top, top);
						bottom = Math.Max(spr.Bottom, bottom);
					}
				}
				offset = new Point(left, top);
				previewPanel.Size = new Size(right - left, bottom - top);
				Application.DoEvents();
				previewGraphics = previewPanel.CreateGraphics();
				previewGraphics.SetOptions();
				bmp = new Bitmap(previewPanel.Width, previewPanel.Height);
				gfx = Graphics.FromImage(bmp);
				gfx.SetOptions();
				curframe = delayframes = 0;
				anitimer.Start();
				DrawPreview();
			}
			else
				anitimer.Stop();
		}

		Bitmap bmp;
		Graphics gfx;
		private Graphics previewGraphics;

		private void DrawPreview()
		{
			if (animation == null || gfx == null) return;
			gfx.Clear(previewPanel.BackColor);
			Sprite spr = sprites[animation[curframe]];
			gfx.DrawImage(spr.Image.ToBitmap(palette), spr.X - offset.X, spr.Y - offset.Y, spr.Width, spr.Height);
			previewGraphics.DrawImage(bmp, 0, 0, previewPanel.Width, previewPanel.Height);
		}

		private void previewPanel_Paint(object sender, PaintEventArgs e) { DrawPreview(); }

		public int CurrentProgress
		{
			get { return progressBar1.Value; }
			set { progressBar1.Value = value; }
		}

		public int MaximumProgress
		{
			get { return progressBar1.Maximum; }
			set { progressBar1.Maximum = value; }
		}
	}
}
