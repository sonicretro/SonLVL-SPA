using System;
using System.Collections.Generic;

namespace SonicRetro.SonLVL.API.SPA
{
	public class Layout : LayoutFormatSeparate
	{
		public System.Drawing.Size LayoutSize;
		
		private void ReadLayoutInternal(byte[] rawdata, ref ushort[,] layout)
		{
			layout = new ushort[MaxSize.Width, MaxSize.Height];
			int c = 0;
			for (int lr = 0; lr < MaxSize.Height; lr++)
				for (int lc = 0; lc < MaxSize.Width; lc++, c += 2)
					layout[lc, lr] = ByteConverter.ToUInt16(rawdata, c);
		}

		public override void ReadFG(byte[] rawdata, LayoutData layout)
		{
			ReadLayoutInternal(rawdata, ref layout.FGLayout);
		}

		public override void ReadBG(byte[] rawdata, LayoutData layout)
		{
			ReadLayoutInternal(rawdata, ref layout.BGLayout);
		}

		private void WriteLayoutInternal(ushort[,] layout, out byte[] rawdata)
		{
			List<byte> tmp = new List<byte>();
			for (int lr = 0; lr < MaxSize.Height; lr++)
				for (int lc = 0; lc < MaxSize.Width; lc++)
					tmp.AddRange(ByteConverter.GetBytes(layout[lc, lr]));
			rawdata = tmp.ToArray();
		}

		public override void WriteFG(LayoutData layout, out byte[] rawdata)
		{
			WriteLayoutInternal(layout.FGLayout, out rawdata);
		}

		public override void WriteBG(LayoutData layout, out byte[] rawdata)
		{
			WriteLayoutInternal(layout.BGLayout, out rawdata);
		}

		//public override System.Drawing.Size MaxSize { get { return new System.Drawing.Size(0x100, 0x80); } }
		public override System.Drawing.Size MaxSize { get { return LayoutSize;} }
	}
}
