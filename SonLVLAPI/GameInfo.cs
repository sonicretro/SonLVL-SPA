using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Reflection;

namespace SonicRetro.SonLVL.API
{
	public class GameInfo
	{
		[IniAlwaysInclude]
		[DefaultValue(EngineVersion.SPA)]
		[IniName("version")]
		public EngineVersion EngineVersion { get; set; }
		[IniName("sprtiles")]
		public string Sprites { get; set; }
		[IniName("palettes")]
		public string GamePalettes { get; set; }
		[IniName("objlst")]
		[IniCollection(IniCollectionMode.SingleLine, Format = "|")]
		public string[] ObjectList { get; set; }
		[IniName("collimg")]
		public string CollisionBitmap { get; set; }
		//[IniName("objartcmp")]
		//[DefaultValue(CompressionType.Nemesis)]
		//public CompressionType ObjectArtCompression { get; set; }
		[IniName("mapver")]
		public EngineVersion MappingsVersion { get; set; }
		[IniName("dplcver")]
		public EngineVersion DPLCVersion { get; set; }
		[IniName("tilefmt")]
		public EngineVersion TileFormat { get; set; }
		[IniName("tilecmp")]
		public CompressionType TileCompression { get; set; }
		[IniName("tilesreserved")]
		public ushort TilesReserved { get; set; }
		[IniName("blockfmt")]
		public EngineVersion BlockFormat { get; set; }
		[IniName("blockcmp")]
		public CompressionType BlockCompression { get; set; }
		[IniName("blockmax")]
		[TypeConverter(typeof(UInt16HexConverter))]
		public ushort? BlockMax { get; set; }
		/*[IniName("chunkfmt")]
		public EngineVersion ChunkFormat { get; set; }
		[IniName("chunksize")]
		public int ChunkSize { get; set; }
		[IniName("chunkwidth")]
		public int ChunkWidth { get; set; }
		[IniName("chunkheight")]
		public int ChunkHeight { get; set; }*/
		[IniName("layoutfmt")]
		public EngineVersion LayoutFormat { get; set; }
		[IniName("layoutcmp")]
		public CompressionType LayoutCompression { get; set; }
		[IniName("fglayoutcmp")]
		public CompressionType FGLayoutCompression { get; set; }
		[IniName("bglayoutcmp")]
		public CompressionType BGLayoutCompression { get; set; }
		[IniName("layoutcodefile")]
		public string LayoutCodeFile { get; set; }
		[IniName("layoutcodetype")]
		public string LayoutCodeType { get; set; }
		[IniName("palettefmt")]
		public EngineVersion PaletteFormat { get; set; }
		[IniName("objectfmt")]
		public EngineVersion ObjectFormat { get; set; }
		[IniName("objectcmp")]
		public CompressionType ObjectCompression { get; set; }
		[IniName("objectcodefile")]
		public string ObjectCodeFile { get; set; }
		[IniName("objectcodetype")]
		public string ObjectCodeType { get; set; }
		/*[IniName("ringfmt")]
		public EngineVersion RingFormat { get; set; }
		[IniName("ringcmp")]
		public CompressionType RingCompression { get; set; }
		[IniName("ringcodefile")]
		public string RingCodeFile { get; set; }
		[IniName("ringcodetype")]
		public string RingCodeType { get; set; }*/
		[IniName("colindfmt")]
		public EngineVersion CollisionIndexFormat { get; set; }
		[IniName("colindcmp")]
		public CompressionType CollisionIndexCompression { get; set; }
		[IniName("colind")]
		public string CollisionIndex { get; set; }
		/*[IniName("colind1")]
		public string CollisionIndex1 { get; set; }
		[IniName("colind2")]
		public string CollisionIndex2 { get; set; }
		[IniName("colarrfmt")]
		public EngineVersion CollisionArrayFormat { get; set; }
		[IniName("colarrcmp")]
		public CompressionType CollisionArrayCompression { get; set; }
		[IniName("colarr1")]
		public string CollisionArray1 { get; set; }
		[IniName("colarr2")]
		public string CollisionArray2 { get; set; }
		[IniName("anglefmt")]
		public EngineVersion AngleFormat { get; set; }
		[IniName("anglecmp")]
		public CompressionType AngleCompression { get; set; }
		[IniName("angles")]
		public string Angles { get; set; }
		[IniName("2pcompat")]
		public bool TwoPlayerCompatible { get; set; }*/
		[IniName("buildscr")]
		public string BuildScript { get; set; }
		[IniName("romfile")]
		public string ROMFile { get; set; }
		[IniName("runcmd")]
		public string RunCommand { get; set; }
		[IniName("useemu")]
		[DefaultValue(true)]
		public bool UseEmulator { get; set; }
		[IniCollection(IniCollectionMode.IndexOnly)]
		public Dictionary<string, LevelInfo> Levels { get; set; }

		public static GameInfo Load(string filename)
		{
			Dictionary<string, Dictionary<string, string>> ini = IniFile.Load(filename);
			string userfile = Path.Combine(Path.GetDirectoryName(filename), Path.GetFileNameWithoutExtension(filename) + ".user" + Path.GetExtension(filename));
			if (File.Exists(userfile))
				ini = IniFile.Combine(ini, IniFile.Load(userfile));
			GameInfo result = IniSerializer.Deserialize<GameInfo>(ini);
			return result;
		}

		public void Save(string filename)
		{
			IniSerializer.Serialize(this, filename);
		}

		public LevelInfo GetLevelInfo(string levelName)
		{
			LevelInfo info = Levels[levelName];
			if (levelName.Contains("\\"))
				levelName = levelName.Substring(levelName.LastIndexOf('\\') + 1);
			LevelInfo result = new LevelInfo();
			foreach (System.Reflection.PropertyInfo prop in typeof(LevelInfo).GetProperties())
			{
				if (prop.PropertyType == typeof(EngineVersion))
					prop.SetValue(result, EngineVersion, null);
				object val;
				PropertyInfo gam = typeof(GameInfo).GetProperty(prop.Name, prop.PropertyType);
				if (gam != null)
				{
					val = gam.GetValue(this, null);
					if (!object.Equals(val, prop.PropertyType.GetDefaultValue()))
						prop.SetValue(result, val, null);
				}
				val = prop.GetValue(info, null);
				if (!object.Equals(val, prop.PropertyType.GetDefaultValue()))
					prop.SetValue(result, val, null);
			}
			if (result.DisplayName == null) result.DisplayName = levelName;
			if (result.TileCompression == CompressionType.Invalid)
				switch (result.TileFormat)
				{
					case EngineVersion.SPA:
					default:
						result.TileCompression = CompressionType.Uncompressed;
						break;
				}
			if (result.BlockCompression == CompressionType.Invalid)
				switch (result.BlockFormat)
				{
					case EngineVersion.SPA:
					default:
						result.BlockCompression = CompressionType.Uncompressed;
						break;
				}
			/*if (result.ChunkCompression == CompressionType.Invalid)
				switch (result.ChunkFormat)
				{
					case EngineVersion.SPA:
					default:
						result.ChunkCompression = CompressionType.Uncompressed;
						break;
				}
			if (result.ChunkSize != 0)
				result.ChunkWidth = result.ChunkHeight = result.ChunkSize;
			else if (result.ChunkWidth == 0 || result.ChunkHeight == 0)
				switch (result.ChunkFormat)
				{
					case EngineVersion.SPA:
						result.ChunkWidth = result.ChunkHeight = 32;
						break;
				}*/
			if (result.FGLayoutCompression == CompressionType.Invalid)
				result.FGLayoutCompression = result.LayoutCompression;
			if (result.BGLayoutCompression == CompressionType.Invalid)
				result.BGLayoutCompression = result.LayoutCompression;
			//result.Palettes = new NamedPaletteList[info.ExtraPalettes.Length + 1];
			//result.Palettes[0] = new NamedPaletteList("Normal", info.Palette);
			//if (info.ExtraPalettes.Length > 0)
			//	info.ExtraPalettes.CopyTo(result.Palettes, 1);
			result.Palettes = new NamedPaletteList[1];
			result.Palettes[0] = new NamedPaletteList("Normal");
			if (result.ObjectCompression == CompressionType.Invalid)
				result.ObjectCompression = CompressionType.Uncompressed;
			//if (result.RingCompression == CompressionType.Invalid)
			//	result.RingCompression = CompressionType.Uncompressed;
			//if (result.BumperCompression == CompressionType.Invalid)
			//	result.BumperCompression = CompressionType.Uncompressed;
			if (result.CollisionIndexCompression == CompressionType.Invalid)
				switch (result.CollisionIndexFormat)
				{
					default:
						result.CollisionIndexCompression = CompressionType.Uncompressed;
						break;
				}
			/*if (result.CollisionArrayCompression == CompressionType.Invalid)
				result.CollisionArrayCompression = CompressionType.Uncompressed;
			if (result.AngleCompression == CompressionType.Invalid)
				result.AngleCompression = CompressionType.Uncompressed;*/
			return result;
		}
	}

	public class LevelInfo
	{
		[IniName("displayname")]
		public string DisplayName { get; set; }
		[IniName("version")]
		public EngineVersion EngineVersion { get; set; }
		[IniName("asmlabel")]
		public string AsmLabel { get; set; }
		[IniName("sizex")]
		public int LevelWidth { get; set; }
		[IniName("sizey")]
		public int LevelHeight { get; set; }
		[IniName("bgpal")]
		[DefaultValue(-1)]
		public int BGPalette { get; set; }
		[IniName("startx")]
		public int StartPosX { get; set; }
		[IniName("starty")]
		public int StartPosY { get; set; }
		[IniName("tilefmt")]
		public EngineVersion TileFormat { get; set; }
		[IniName("tilecmp")]
		public CompressionType TileCompression { get; set; }
		[IniName("tilesreserved")]
		public ushort TilesReserved { get; set; }
		[IniName("tiles")]
		[IniCollection(IniCollectionMode.SingleLine, Format = "|")]
		public FileInfo[] Tiles { get; set; }
		[IniName("blockfmt")]
		public EngineVersion BlockFormat { get; set; }
		[IniName("blockcmp")]
		public CompressionType BlockCompression { get; set; }
		[IniName("blocks")]
		[IniCollection(IniCollectionMode.SingleLine, Format = "|")]
		public FileInfo[] Blocks { get; set; }
		//[IniName("chunkfmt")]
		//public EngineVersion ChunkFormat { get; set; }
		//[IniName("chunkcmp")]
		//public CompressionType ChunkCompression { get; set; }
		//[IniName("chunksize")]
		//public int ChunkSize { get; set; }
		//[IniName("chunkwidth")]
		//public int ChunkWidth { get; set; }
		//[IniName("chunkheight")]
		//public int ChunkHeight { get; set; }
		//[IniName("chunks")]
		//[IniCollection(IniCollectionMode.SingleLine, Format = "|")]
		//public FileInfo[] Chunks { get; set; }
		[IniName("layoutfmt")]
		public EngineVersion LayoutFormat { get; set; }
		[IniName("layoutcmp")]
		public CompressionType LayoutCompression { get; set; }
		[IniName("fglayoutcmp")]
		public CompressionType FGLayoutCompression { get; set; }
		[IniName("bglayoutcmp")]
		public CompressionType BGLayoutCompression { get; set; }
		[IniName("layoutcodefile")]
		public string LayoutCodeFile { get; set; }
		[IniName("layoutcodetype")]
		public string LayoutCodeType { get; set; }
		[IniName("layout")]
		public string Layout { get; set; }
		[IniName("fglayout")]
		public string FGLayout { get; set; }
		[IniName("bglayout")]
		public string BGLayout { get; set; }
		[IniName("palettefmt")]
		public EngineVersion PaletteFormat { get; set; }
		[IniIgnore]
		public NamedPaletteList[] Palettes { get; set; }
		[IniName("fgpalette")]
		public string FGPaletteList { get; set; }
		[IniName("bgpalette")]
		public string BGPaletteList { get; set; }
		//[IniName("palette")]
		//[IniCollection(IniCollectionMode.NoSquareBrackets, StartIndex = 2)]
		//public NamedPaletteList[] ExtraPalettes { get; set; }
		//[DefaultValue(0)]
		//[IniName("waterpal")]
		//public int WaterPalette { get; set; }
		//[DefaultValue(0x600)]
		[IniName("waterheight")]
		[TypeConverter(typeof(Int32HexConverter))]
		public int WaterHeight { get; set; }
		[IniName("objectfmt")]
		public EngineVersion ObjectFormat { get; set; }
		[IniName("objectcmp")]
		public CompressionType ObjectCompression { get; set; }
		[IniName("objectcodefile")]
		public string ObjectCodeFile { get; set; }
		[IniName("objectcodetype")]
		public string ObjectCodeType { get; set; }
		[IniName("objects")]
		public string Objects { get; set; }
		/*[IniName("ringfmt")]
		public EngineVersion RingFormat { get; set; }
		[IniName("ringcmp")]
		public CompressionType RingCompression { get; set; }
		[IniName("ringcodefile")]
		public string RingCodeFile { get; set; }
		[IniName("ringcodetype")]
		public string RingCodeType { get; set; }
		[IniName("rings")]
		public string Rings { get; set; }
		[IniName("bumpercmp")]
		public CompressionType BumperCompression { get; set; }
		[IniName("bumpers")]
		public string Bumpers { get; set; }*/
		[IniName("startpos")]
		[IniCollection(IniCollectionMode.SingleLine, Format = "|")]
		public StartPositionInfo[] StartPositions { get; set; }
		[IniName("colindfmt")]
		public EngineVersion CollisionIndexFormat { get; set; }
		[IniName("colindcmp")]
		public CompressionType CollisionIndexCompression { get; set; }
		[IniName("colind")]
		public string CollisionIndex { get; set; }
		/*[IniName("colind1")]
		public string CollisionIndex1 { get; set; }
		[IniName("colind2")]
		public string CollisionIndex2 { get; set; }
		[IniName("colindsz")]
		public int CollisionIndexSize { get; set; }
		[IniName("colarrfmt")]
		public EngineVersion CollisionArrayFormat { get; set; }
		[IniName("colarrcmp")]
		public CompressionType CollisionArrayCompression { get; set; }
		[IniName("colarr1")]
		public string CollisionArray1 { get; set; }
		[IniName("colarr2")]
		public string CollisionArray2 { get; set; }
		[IniName("anglefmt")]
		public EngineVersion AngleFormat { get; set; }
		[IniName("anglecmp")]
		public CompressionType AngleCompression { get; set; }
		[IniName("angles")]
		public string Angles { get; set; }
		[IniName("timezone")]
		public TimeZone TimeZone { get; set; }
		[IniCollection(IniCollectionMode.SingleLine, Format = ",", ValueConverter = typeof(ByteHexConverter))]
		[IniName("loopchunks")]
		public List<byte> LoopChunks { get; set; }
		[IniName("2pcompat")]
		public bool TwoPlayerCompatible { get; set; }*/
		[IniName("sprtiles")]
		public string Sprites { get; set; }
		[IniName("palettes")]
		public string GamePalettes { get; set; }
		[IniName("objlst")]
		[IniCollection(IniCollectionMode.SingleLine, Format = "|")]
		public string[] ObjectList { get; set; }
		
	}

	[TypeConverter(typeof(StringConverter<FileInfo>))]
	public class FileInfo
	{
		public string Filename;
		public int Offset;

		public FileInfo(string data)
		{
			string[] split = data.Split(':');
			Filename = split[0];
			Offset = -1;
			if (split.Length > 1)
			{
				string offstr = split[1];
				if (offstr.StartsWith("0x"))
					Offset = int.Parse(offstr.Substring(2), NumberStyles.HexNumber);
				else
					Offset = int.Parse(offstr, NumberStyles.Integer, NumberFormatInfo.InvariantInfo);
			}
		}

		public override string ToString()
		{
			if (Offset != -1)
				return Filename + ":0x" + Offset.ToString("X");
			else
				return Filename;
		}

		public override bool Equals(object obj)
		{
			return obj is FileInfo && Filename == ((FileInfo)obj).Filename && Offset == ((FileInfo)obj).Offset;
		}

		public override int GetHashCode()
		{
			return Filename.GetHashCode() ^ Offset.GetHashCode();
		}
	}

	[TypeConverter(typeof(StringConverter<NamedPaletteList>))]
	public class NamedPaletteList : IEnumerable<PaletteInfo>
	{
		public string Name;
		public PaletteList Palettes;

		public NamedPaletteList(string data)
		{
			string[] files = data.Split('|');
			Name = files[0];
			if (files.Length > 1)
				Palettes = new PaletteList(string.Join("|", files, 1, files.Length - 1));
			else
				Palettes = null;
		}

		public NamedPaletteList(string name, PaletteList paletteList)
		{
			Name = name;
			Palettes = paletteList;
		}

		public override string ToString()
		{
			return Name + "|" + Palettes.ToString();
		}

		IEnumerator<PaletteInfo> IEnumerable<PaletteInfo>.GetEnumerator()
		{
			return new List<PaletteInfo>(Palettes.Collection).GetEnumerator();
		}

		System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
		{
			return Palettes.Collection.GetEnumerator();
		}

		public PaletteInfo this[int index]
		{
			get { return Palettes.Collection[index]; }
			set { Palettes.Collection[index] = value; }
		}
	}

	[TypeConverter(typeof(StringConverter<PaletteList>))]
	public class PaletteList : IEnumerable<PaletteInfo>
	{
		public PaletteInfo[] Collection;

		public PaletteList(string data)
		{
			string[] files = data.Split('|');
			List<PaletteInfo> filelist = new List<PaletteInfo>();
			foreach (string item in files)
				filelist.Add(new PaletteInfo(item));
			Collection = filelist.ToArray();
		}

		public override string ToString()
		{
			List<string> data = new List<string>(Collection.Length);
			foreach (PaletteInfo item in Collection)
				data.Add(item.ToString());
			return string.Join("|", data.ToArray());
		}

		IEnumerator<PaletteInfo> IEnumerable<PaletteInfo>.GetEnumerator()
		{
			return new List<PaletteInfo>(Collection).GetEnumerator();
		}

		System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
		{
			return Collection.GetEnumerator();
		}

		public PaletteInfo this[int index]
		{
			get { return Collection[index]; }
			set { Collection[index] = value; }
		}
	}

	public class PaletteInfo
	{
		public string Filename;
		public int Source, Destination, Length;

		public PaletteInfo(string data)
		{
			string[] split = data.Split(':');
			Filename = split[0];
			Source = int.Parse(split[1], NumberStyles.Integer, NumberFormatInfo.InvariantInfo);
			Destination = int.Parse(split[2], NumberStyles.Integer, NumberFormatInfo.InvariantInfo);
			Length = int.Parse(split[3], NumberStyles.Integer, NumberFormatInfo.InvariantInfo);
		}

		public override string ToString()
		{
			return Filename + ":" + Source.ToString(NumberFormatInfo.InvariantInfo) + ":" + Destination.ToString(NumberFormatInfo.InvariantInfo) + ":" + Length.ToString(NumberFormatInfo.InvariantInfo);
		}
	}

	[TypeConverter(typeof(StringConverter<StartPositionInfo>))]
	public class StartPositionInfo
	{
		public string Filename, Sprite, Name;
		public int Offset = -1;

		public StartPositionInfo(string data)
		{
			string[] split = data.Split(':');
			Filename = split[0];
			Sprite = split[1];
			Name = split[2];
			if (split.Length > 3)
				Offset = int.Parse(split[3], NumberStyles.HexNumber);
		}

		public override string ToString()
		{
			return Filename + ":" + Sprite + ":" + Name + (Offset == -1 ? "" : ":" + Offset.ToString("X"));
		}
	}

	/// <summary>
	/// Converts between <see cref="System.String"/> and <typeparamref name="T"/>
	/// </summary>
	public class StringConverter<T> : TypeConverter
	{
		public override bool CanConvertTo(ITypeDescriptorContext context, Type destinationType)
		{
			if (destinationType == typeof(T))
				return true;
			return base.CanConvertTo(context, destinationType);
		}

		public override object ConvertTo(ITypeDescriptorContext context, CultureInfo culture, object value, Type destinationType)
		{
			if (destinationType == typeof(string) && value is T)
				return ((T)value).ToString();
			return base.ConvertTo(context, culture, value, destinationType);
		}

		public override bool CanConvertFrom(ITypeDescriptorContext context, Type sourceType)
		{
			if (sourceType == typeof(string))
				return true;
			return base.CanConvertFrom(context, sourceType);
		}

		public override object ConvertFrom(ITypeDescriptorContext context, CultureInfo culture, object value)
		{
			if (value is string)
				return Activator.CreateInstance(typeof(T), (string)value);
			return base.ConvertFrom(context, culture, value);
		}
	}

	public class Int32HexConverter : TypeConverter
	{
		public override bool CanConvertTo(ITypeDescriptorContext context, Type destinationType)
		{
			if (destinationType == typeof(string))
				return true;
			return base.CanConvertTo(context, destinationType);
		}

		public override object ConvertTo(ITypeDescriptorContext context, CultureInfo culture, object value, Type destinationType)
		{
			if (destinationType == typeof(string) && value is int)
				return ((int)value).ToString("X");
			return base.ConvertTo(context, culture, value, destinationType);
		}

		public override bool CanConvertFrom(ITypeDescriptorContext context, Type sourceType)
		{
			if (sourceType == typeof(string))
				return true;
			return base.CanConvertFrom(context, sourceType);
		}

		public override object ConvertFrom(ITypeDescriptorContext context, CultureInfo culture, object value)
		{
			if (value is string)
				return int.Parse((string)value, NumberStyles.HexNumber);
			return base.ConvertFrom(context, culture, value);
		}

		public override bool IsValid(ITypeDescriptorContext context, object value)
		{
			if (value is int)
				return true;
			int i;
			if (value is string)
				return int.TryParse((string)value, NumberStyles.HexNumber, NumberFormatInfo.InvariantInfo, out i);
			return base.IsValid(context, value);
		}
	}

	public class UInt16HexConverter : TypeConverter
	{
		public override bool CanConvertTo(ITypeDescriptorContext context, Type destinationType)
		{
			if (destinationType == typeof(string))
				return true;
			return base.CanConvertTo(context, destinationType);
		}

		public override object ConvertTo(ITypeDescriptorContext context, CultureInfo culture, object value, Type destinationType)
		{
			if (destinationType == typeof(string) && value is ushort)
				return ((ushort)value).ToString("X");
			return base.ConvertTo(context, culture, value, destinationType);
		}

		public override bool CanConvertFrom(ITypeDescriptorContext context, Type sourceType)
		{
			if (sourceType == typeof(string))
				return true;
			return base.CanConvertFrom(context, sourceType);
		}

		public override object ConvertFrom(ITypeDescriptorContext context, CultureInfo culture, object value)
		{
			if (value is string)
				return ushort.Parse((string)value, NumberStyles.HexNumber);
			return base.ConvertFrom(context, culture, value);
		}

		public override bool IsValid(ITypeDescriptorContext context, object value)
		{
			if (value is ushort)
				return true;
			ushort i;
			if (value is string)
				return ushort.TryParse((string)value, NumberStyles.HexNumber, NumberFormatInfo.InvariantInfo, out i);
			return base.IsValid(context, value);
		}
	}

	public class ByteHexConverter : TypeConverter
	{
		public override bool CanConvertTo(ITypeDescriptorContext context, Type destinationType)
		{
			if (destinationType == typeof(string))
				return true;
			return base.CanConvertTo(context, destinationType);
		}

		public override object ConvertTo(ITypeDescriptorContext context, CultureInfo culture, object value, Type destinationType)
		{
			if (destinationType == typeof(string) && value is byte)
				return ((byte)value).ToString("X");
			return base.ConvertTo(context, culture, value, destinationType);
		}

		public override bool CanConvertFrom(ITypeDescriptorContext context, Type sourceType)
		{
			if (sourceType == typeof(string))
				return true;
			return base.CanConvertFrom(context, sourceType);
		}

		public override object ConvertFrom(ITypeDescriptorContext context, CultureInfo culture, object value)
		{
			if (value is string)
				return byte.Parse((string)value, NumberStyles.HexNumber);
			return base.ConvertFrom(context, culture, value);
		}

		public override bool IsValid(ITypeDescriptorContext context, object value)
		{
			if (value is byte)
				return true;
			byte i;
			if (value is string)
				return byte.TryParse((string)value, NumberStyles.HexNumber, NumberFormatInfo.InvariantInfo, out i);
			return base.IsValid(context, value);
		}
	}
}
