using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.ComponentModel;
using SonicRetro.SonLVL.API;
using System.IO;
using System.Globalization;

namespace SonicRetro.SonLVL
{
	[TypeConverter(typeof(StringConverter<AnimationIFrame>))]
	public class AnimationIFrame
	{
		public string Sprite;
		public byte Speed;

		public AnimationIFrame(string data)
		{
			string[] split = data.Split(':');
			Sprite = split[0];
			if (split.Length > 1)
			{
				if (split[1].ToLower().StartsWith("0x"))
					Speed = byte.Parse(split[1].Substring(2), NumberStyles.HexNumber);
				else
					Speed = byte.Parse(split[1], NumberStyles.Integer);
			}
		}

		public override string ToString()
		{
			if (Speed >= 0xF0)
				return Sprite + ":" + "0x" + Speed.ToString("X2");
			else
				return Sprite + ":" + Speed.ToString();
		}
	}
	
	public class AnimationInfo
	{
		[DefaultValue(EngineVersion.SPA)]
		[IniName("game")]
		public EngineVersion Game { get; set; }
		[IniName("palettes")]
		public string GamePalettes { get; set; }
		[IniName("sprtiles")]
		public string Sprites { get; set; }
		[IniName("flags")]
		[DefaultValue((byte)0x10)]
		public byte Flags;
		[IniName("pal1")]
		[DefaultValue((short)-1)]
		public short Palette1;
		[IniName("pal2")]
		[DefaultValue((short)-1)]
		public short Palette2;
		[IniName("animation")]
		[IniCollection(IniCollectionMode.SingleLine, Format = "|")]
		public AnimationIFrame[] Frames { get; set; }
		[IniIgnore]
		public Animation Animation { get; set; }
		[IniIgnore]
		public List<SpriteInfo[]> SprList { get; set; }
		/*[IniName("map")]
		public string MappingsFile { get; set; }
		[IniName("mapgame")]
		public EngineVersion MappingsGame { get; set; }
		[IniName("mapfmt")]
		public MappingsFormat MappingsFormat { get; set; }
		[IniName("dplc")]
		public string DPLCFile { get; set; }
		[IniName("dplcgame")]
		public EngineVersion DPLCGame { get; set; }
		[IniName("dplcfmt")]
		public MappingsFormat DPLCFormat { get; set; }
		[IniName("anim")]
		public string AnimationFile { get; set; }
		[IniName("animfmt")]
		public MappingsFormat AnimationFormat { get; set; }
		[IniName("startpal")]
		public int StartPalette { get; set; }*/

		public static Dictionary<string, AnimationInfo> Load(string filename)
		{
			Dictionary<string, Dictionary<string, string>> ini = IniFile.Load(filename);
			string userfile = Path.Combine(Path.GetDirectoryName(filename), Path.GetFileNameWithoutExtension(filename) + ".user" + Path.GetExtension(filename));
			if (File.Exists(userfile))
				ini = IniFile.Combine(ini, IniFile.Load(userfile));
			Dictionary<string, AnimationInfo> result = IniSerializer.Deserialize<Dictionary<string, AnimationInfo>>(ini);
			foreach (KeyValuePair<string, AnimationInfo> anim in result)
			{
				/*if (anim.Value.MappingsGame == EngineVersion.Invalid)
					anim.Value.MappingsGame = anim.Value.Game;
				if (anim.Value.DPLCGame == EngineVersion.Invalid)
					anim.Value.DPLCGame = anim.Value.Game;
				if (anim.Value.MappingsFormat == MappingsFormat.Invalid)
					anim.Value.MappingsFormat = MappingsFormat.Binary;
				if (anim.Value.DPLCFormat == MappingsFormat.Invalid)
					anim.Value.DPLCFormat = MappingsFormat.Binary;
				if (anim.Value.AnimationFormat == MappingsFormat.Invalid)
					anim.Value.AnimationFormat = MappingsFormat.Binary;*/
				Dictionary<string, string> inisection = ini[anim.Key];
				List<byte> frmspd = new List<byte>();
				Dictionary<string, int> frmspr = new Dictionary<string, int>();
				anim.Value.SprList = new List<SpriteInfo[]>();
				for (int i = 0; i < anim.Value.Frames.Length; i ++)
				{
					string sprName = anim.Value.Frames[i].Sprite;
					if (anim.Value.Frames[i].Speed >= 0xF0)
					{
						frmspd.Add(0);
						frmspd.Add(anim.Value.Frames[i].Speed);
						break;
					}
					
					if (! frmspr.ContainsKey(sprName))
					{
						frmspr[sprName] = anim.Value.SprList.Count;
						
						string[] vals = inisection[sprName].Split('|');
						SpriteInfo[] sprinfs = new SpriteInfo[vals.Length];
						for (int j = 0; j < vals.Length; j++)
							sprinfs[j] = new SpriteInfo(vals[j]);
						anim.Value.SprList.Add(sprinfs);
					}
					frmspd.Add((byte)frmspr[sprName]);
					frmspd.Add((byte)(anim.Value.Frames[i].Speed * 2));
				}
				anim.Value.Animation = new Animation(frmspd.ToArray(), 0, anim.Key + "_Load");
			}
			return result;
		}
	}
}
