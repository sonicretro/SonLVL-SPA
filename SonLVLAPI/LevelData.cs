using System;
using System.CodeDom;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Windows.Forms;

namespace SonicRetro.SonLVL.API
{
	public static class LevelData
	{
		public static GameInfo Game;
		public static LevelInfo Level;
		public static ushort TilesReserved;
		public static MultiFileIndexer<byte[]> Tiles;
		public static byte[] TileArray;
		public static MultiFileIndexer<Block> Blocks;
		public static List<BitmapBits[]> BlockBmpBits;
		//public static List<BitmapBits> CompBlockBmpBits;
		public static List<Bitmap[]> BlockBmps;
		public static List<Bitmap>[] BlockBmpsA;	// [0] - background, [1] - foreground
		//public static List<Bitmap> CompBlockBmps;
		//public static MultiFileIndexer<Chunk> Chunks;
		//public static List<BitmapBits[]> ChunkBmpBits;
		//public static List<BitmapBits> CompChunkBmpBits;
		//public static List<Bitmap[]> ChunkBmps;
		//public static List<Bitmap> CompChunkBmps;
		public static LayoutData Layout;
		public static Dictionary<string, KeyValuePair<CompressionType, LayoutData>> AdditionalLayouts;
		public static LayoutFormat LayoutFormat;
		public static byte[] SpriteTiles;
		public static SonLVLColor[] PalCollection;
		public static List<string> PalName;
		public static SonLVLColor BGColor;
		public static List<SonLVLColor[,]> Palette;
		//public static List<byte[,]> PalNum;
		//public static List<int[,]> PalAddr;
		public static PaletteIDList PalIDsFG;
		public static PaletteIDList PalIDsBG;
		public static int CurPal;
		public static ColorPalette BmpPal;
		public static byte BmpPalColMax;
		public static byte[] BmpPalColLUT;		// Palette ID/Color -> Bitmap Palette Entry
		public static List<ObjectEntry> Objects;
		static bool objectterm;
		public static ObjectLayoutFormat ObjectFormat;
		//public static List<RingEntry> Rings;
		//internal static bool ringstartterm, ringendterm;
		public static RingFormat RingFormat;
		//public static List<CNZBumperEntry> Bumpers;
		public static List<StartPositionEntry> StartPositions;
		public static Dictionary<string, ObjectData> INIObjDefs;
		public static Dictionary<byte, ObjectDefinition> ObjTypes;
		public static ObjectDefinition unkobj;
		public static List<StartPositionDefinition> StartPosDefs;
		public static bool littleendian;
		public static Dictionary<string, byte[]> filecache;
		public static List<short> ColInds;	// tile collision indices
		//public static List<byte> ColInds1;
		//public static List<byte> ColInds2;
		//public static sbyte[][] ColArr1;
		//public static byte[] Angles;
		public static BitmapBits[] ColBmpBits;
		public static Bitmap[] ColBmps;
		public static List<BitmapBits> BlockColBmpBits;
		public static List<Bitmap> BlockColBmps;
		//public static List<BitmapBits[]> ChunkColBmpBits;
		//public static List<Bitmap[]> ChunkColBmps;
		public static Bitmap UnknownImg;
		public static List<Sprite> Sprites;
		public delegate void LogEventHandler(params string[] message);
		public static event LogEventHandler LogEvent = delegate { };
		public static event Action PaletteChangedEvent = delegate { };
		internal static readonly bool IsMonoRuntime = Type.GetType("Mono.Runtime") != null;
		internal static readonly bool IsWindows = !(Environment.OSVersion.Platform == PlatformID.MacOSX | Environment.OSVersion.Platform == PlatformID.Unix | Environment.OSVersion.Platform == PlatformID.Xbox);
		private static readonly BitmapBits InvalidTile = new BitmapBits(8, 8);
		private static readonly BitmapBits InvalidBlock = new BitmapBits(32, 32);
		public const int ColorTransparent = 0;
		public const int ColorFirstGlobal = 251;
		public const int ColorRed = 251;
		public const int ColorWhite = 252;
		public const int ColorYellow = 253;
		public const int ColorBlack = 254;
		public const int ColorGrid = 255;

		static LevelData()
		{
			InvalidTile.DrawLine(3, 0, 0, 7, 0);
			InvalidTile.DrawLine(3, 0, 0, 0, 7);
			InvalidTile.DrawLine(3, 7, 7, 0, 7);
			InvalidTile.DrawLine(3, 7, 7, 7, 0);
			InvalidTile.DrawLine(3, 0, 0, 7, 7);
			InvalidTile.DrawLine(3, 0, 7, 7, 0);
			InvalidBlock.DrawLine(3, 0, 0, 31, 0);
			InvalidBlock.DrawLine(3, 0, 0, 0, 31);
			InvalidBlock.DrawLine(3, 31, 31, 0, 31);
			InvalidBlock.DrawLine(3, 31, 31, 31, 0);
			InvalidBlock.DrawLine(3, 0, 0, 15, 31);
			InvalidBlock.DrawLine(3, 0, 31, 31, 0);
		}

		public static void LoadGame(string filename)
		{
			Log("Opening INI file \"" + filename + "\"...");
			Game = GameInfo.Load(filename);
			Environment.CurrentDirectory = Path.GetDirectoryName(filename);
			switch (Game.EngineVersion)
			{
				case EngineVersion.SPA:
					UnknownImg = Properties.Resources.UnknownImgSPA.Copy();
					littleendian = true;
					break;
				default:
					throw new NotImplementedException("Game type " + Game.EngineVersion.ToString() + " is not supported!");
			}
			Log("Game type is " + Game.EngineVersion.ToString() + ".");
		}

		public static void LoadLevel(string levelname, bool loadGraphics)
		{
			Level = Game.GetLevelInfo(levelname);
			switch (Level.EngineVersion)
			{
				case EngineVersion.SPA:
					UnknownImg = Properties.Resources.UnknownImgSPA.Copy();
					littleendian = true;
					break;
				default:
					throw new NotImplementedException("Game type " + Level.EngineVersion.ToString() + " is not supported!");
			}
			Log("Loading " + Level.DisplayName + "...");
			/*if ((Level.ChunkWidth & 15) != 0)
				throw new ArgumentException("Chunk width must be divisible by 16!");
			if ((Level.ChunkHeight & 15) != 0)
				throw new ArgumentException("Chunk height must be divisible by 16!");*/
			TilesReserved = Level.TilesReserved;
			byte[] tmp = null;
			List<byte> data = new List<byte>();
			Tiles = new MultiFileIndexer<byte[]>(() => new byte[32]);
			//if (Level.TileFormat != EngineVersion.SCDPC)
			{
				foreach (FileInfo tileent in Level.Tiles)
				{
					tmp = null;
					if (File.Exists(tileent.Filename))
					{
						Log("Loading 8x8 tiles from file \"" + tileent.Filename + "\", using compression " + Level.TileCompression.ToString() + "...");
						tmp = Compression.Decompress(tileent.Filename, Level.TileCompression);
						Pad(ref tmp, 16);
						List<byte[]> tiles = new List<byte[]>();
						for (int i = 0; i < tmp.Length; i += 0x10)
						{
							byte[] tile = new byte[0x10];
							Array.Copy(tmp, i, tile, 0, 0x10);
							tiles.Add(tile);
						}
						Tiles.AddFile(tiles, tileent.Offset == -1 ? tileent.Offset : tileent.Offset / 0x10);
					}
					else
					{
						Log("8x8 tile file \"" + tileent.Filename + "\" not found.");
						Tiles.AddFile(new List<byte[]>() { new byte[0x10] }, tileent.Offset == -1 ? tileent.Offset : tileent.Offset / 0x10);
					}
				}
			}
			Tiles.FillGaps();
			UpdateTileArray();
			Blocks = new MultiFileIndexer<Block>(() => new Block());
			foreach (FileInfo tileent in Level.Blocks)
			{
				if (File.Exists(tileent.Filename))
				{
					Log("Loading 32x32 blocks from file \"" + tileent.Filename + "\", using compression " + Level.BlockCompression.ToString() + "...");
					tmp = Compression.Decompress(tileent.Filename, Level.BlockCompression);
					Pad(ref tmp, Block.Size);
					List<Block> tmpblk = new List<Block>();
					if (Level.EngineVersion == EngineVersion.SKC)
						littleendian = false;
					for (int ba = 0; ba < tmp.Length; ba += Block.Size)
						tmpblk.Add(new Block(tmp, ba));
					if (Level.EngineVersion == EngineVersion.SKC)
						littleendian = true;
					Blocks.AddFile(tmpblk, tileent.Offset == -1 ? tileent.Offset : tileent.Offset / Block.Size);
				}
				else
				{
					Log("32x32 block file \"" + tileent.Filename + "\" not found.");
					Blocks.AddFile(new List<Block>() { new Block() }, tileent.Offset == -1 ? tileent.Offset : tileent.Offset / Block.Size);
				}
			}
			if (Blocks.Count == 0)
				Blocks.AddFile(new List<Block>() { new Block() }, -1);
			Blocks.FillGaps();
			/*Chunks = new MultiFileIndexer<Chunk>(() => new Chunk());
			data = new List<byte>();
			int fileind = 0;
			foreach (FileInfo tileent in Level.Chunks)
			{
				if (File.Exists(tileent.Filename))
				{
					Log("Loading " + Level.ChunkWidth + "x" + Level.ChunkHeight + " chunks from file \"" + tileent.Filename + "\", using compression " + Level.ChunkCompression.ToString() + "...");
					tmp = Compression.Decompress(tileent.Filename, Level.ChunkCompression);
					Pad(ref tmp, Chunk.Size);
					List<Chunk> tmpchnk = new List<Chunk>();
					if (fileind == 0)
					{
						switch (Level.ChunkFormat)
						{
							case EngineVersion.S1:
							case EngineVersion.SCD:
							case EngineVersion.SCDPC:
								tmpchnk.Add(new Chunk());
								break;
						}
					}
					if (Level.EngineVersion == EngineVersion.SKC)
						littleendian = false;
					for (int ba = 0; ba < tmp.Length; ba += Chunk.Size)
						tmpchnk.Add(new Chunk(tmp, ba));
					if (Level.EngineVersion == EngineVersion.SKC)
						littleendian = true;
					Chunks.AddFile(tmpchnk, tileent.Offset == -1 ? tileent.Offset : tileent.Offset / Chunk.Size);
					fileind++;
				}
				else
				{
					Log(Level.ChunkWidth + "x" + Level.ChunkHeight + " chunk file \"" + tileent.Filename + "\" not found.");
					Chunks.AddFile(new List<Chunk>() { new Chunk() }, tileent.Offset == -1 ? tileent.Offset : tileent.Offset / Chunk.Size);
				}
			}
			if (Chunks.Count == 0)
				Chunks.AddFile(new List<Chunk>() { new Chunk() }, -1);
			Chunks.FillGaps();*/
			Layout = new LayoutData();
			AdditionalLayouts = new Dictionary<string,KeyValuePair<CompressionType,LayoutData>>();
			switch (Level.LayoutFormat)
			{
				/*case EngineVersion.S1:
				case EngineVersion.SCD:
					LayoutFormat = new S1.Layout();
					break;
				case EngineVersion.S2NA:
					LayoutFormat = new S2NA.Layout();
					break;
				case EngineVersion.S2:
					LayoutFormat = new S2.Layout();
					break;
				case EngineVersion.S3K:
					LayoutFormat = new S3K.Layout();
					break;
				case EngineVersion.SKC:
					LayoutFormat = new SKC.Layout();
					break;
				case EngineVersion.SCDPC:
					LayoutFormat = new SCDPC.Layout();
					break;*/
				case EngineVersion.SPA:
					LayoutFormat = new SPA.Layout();
					((SPA.Layout)LayoutFormat).LayoutSize = new Size(Level.LevelWidth, Level.LevelHeight);
					break;
				case EngineVersion.Custom:
					LayoutFormat = CompileCodeFile<LayoutFormat>(Level.LayoutCodeFile, Level.LayoutCodeType);
					break;
			}
			if (LayoutFormat.IsCombinedLayout)
			{
				LayoutFormatCombined lfc = (LayoutFormatCombined)LayoutFormat;
				lfc.TryReadLayout(Level.Layout, Level.LayoutCompression, Layout);
				foreach (string lvlname in Game.Levels.Keys)
					if (lvlname != levelname)
					{
						LevelInfo lvlinf = Game.GetLevelInfo(lvlname);
						if (Level.Layout != lvlinf.Layout && !AdditionalLayouts.ContainsKey(lvlinf.Layout)
							&& Level.LayoutFormat == lvlinf.LayoutFormat && Level.Blocks.ArrayEqual(lvlinf.Blocks))
						{
							LayoutData ld = new LayoutData();
							lfc.TryReadLayout(lvlinf.Layout, lvlinf.LayoutCompression, ld);
							AdditionalLayouts.Add(lvlinf.Layout, new KeyValuePair<CompressionType, LayoutData>(lvlinf.LayoutCompression, ld));
						}
					}
			}
			else
			{
				LayoutFormatSeparate lfs = (LayoutFormatSeparate)LayoutFormat;
				lfs.TryReadLayout(Level.FGLayout, Level.BGLayout, Level.FGLayoutCompression, Level.BGLayoutCompression, Layout);
				foreach (string lvlname in Game.Levels.Keys)
					if (lvlname != levelname)
					{
						LevelInfo lvlinf = Game.GetLevelInfo(lvlname);
						if (Level.LayoutFormat == lvlinf.LayoutFormat && Level.Blocks.ArrayEqual(lvlinf.Blocks))
						{
							if (Level.LayoutFormat == EngineVersion.SPA)
								((SPA.Layout)lfs).LayoutSize = new Size(lvlinf.LevelWidth, lvlinf.LevelHeight);
							if (Level.FGLayout != lvlinf.FGLayout && !AdditionalLayouts.ContainsKey(lvlinf.FGLayout))
							{
								LayoutData ld = new LayoutData();
								lfs.TryReadFG(lvlinf.FGLayout, lvlinf.FGLayoutCompression, ld);
								AdditionalLayouts.Add(lvlinf.FGLayout, new KeyValuePair<CompressionType, LayoutData>(lvlinf.FGLayoutCompression, ld));
							}
							if (Level.BGLayout != lvlinf.BGLayout && !AdditionalLayouts.ContainsKey(lvlinf.BGLayout))
							{
								LayoutData ld = new LayoutData();
								lfs.TryReadBG(lvlinf.BGLayout, lvlinf.BGLayoutCompression, ld);
								AdditionalLayouts.Add(lvlinf.BGLayout, new KeyValuePair<CompressionType, LayoutData>(lvlinf.BGLayoutCompression, ld));
							}
						}
					}
			}
			/*PalName = new List<string>();
			Palette = new List<SonLVLColor[,]>();
			PalNum = new List<byte[,]>();
			PalAddr = new List<int[,]>();
			byte palfilenum = 0;
			for (int palnum = 0; palnum < Level.Palettes.Length; palnum++)
			{
				PalName.Add(Level.Palettes[palnum].Name);
				Palette.Add(new SonLVLColor[4, 16]);
				PalNum.Add(new byte[4, 16]);
				PalAddr.Add(new int[4, 16]);
				for (byte pn = 0; pn < Level.Palettes[palnum].Palettes.Collection.Length; pn++)
				{
					PaletteInfo palent = Level.Palettes[palnum].Palettes[pn];
					Log("Loading palette file \"" + palent.Filename + "\"...", "Source: " + palent.Source + " Destination: " + palent.Destination + " Length: " + palent.Length);
					if (!File.Exists(palent.Filename)) throw new FileNotFoundException("Palette file could not be loaded! Have you set up your disassembly properly?", palent.Filename);
					tmp = File.ReadAllBytes(palent.Filename);
					SonLVLColor[] palfile;
					if (Level.PaletteFormat != EngineVersion.SCDPC)
					{
						palfile = new SonLVLColor[tmp.Length / 2];
						for (int pi = 0; pi < tmp.Length; pi += 2)
							palfile[pi / 2] = new SonLVLColor(ByteConverter.ToUInt16(tmp, pi));
					}
					else
					{
						palfile = new SonLVLColor[tmp.Length / 4];
						for (int pi = 0; pi < tmp.Length; pi += 4)
							palfile[pi / 4] = new SonLVLColor(tmp[pi], tmp[pi + 1], tmp[pi + 2]);
					}
					for (int pa = 0; pa < palent.Length; pa++)
					{
						Palette[palnum][(pa + palent.Destination) / 16, (pa + palent.Destination) % 16] = palfile[pa + palent.Source];
						PalNum[palnum][(pa + palent.Destination) / 16, (pa + palent.Destination) % 16] = palfilenum;
						PalAddr[palnum][(pa + palent.Destination) / 16, (pa + palent.Destination) % 16] = pa + palent.Source;
					}
					palfilenum++;
				}
			}*/
			PalCollection = SonLVLColor.Load(Level.GamePalettes, Level.EngineVersion);
			BmpPalColLUT = new byte[PalCollection.Length];
			PalIDsFG = PaletteIDList.Load(Level.FGPaletteList, Level.EngineVersion);
			PalIDsBG = PaletteIDList.Load(Level.BGPaletteList, Level.EngineVersion);
			PalName = new List<string>();
			Palette = new List<SonLVLColor[,]>();
			if (Level.BGPalette != -1)
				BGColor = PalCollection[Level.BGPalette * 3];
			else
				BGColor = new SonLVLColor(Color.Black);
			for (int palnum = 0; palnum < Level.Palettes.Length; palnum++)
			{
				PalName.Add(Level.Palettes[palnum].Name);
				Palette.Add(new SonLVLColor[32, 4]);
				for (int pa = 0; pa < 16; pa++)
				{
					ushort fgPalID = PalIDsFG.palIDs[pa];
					ushort bgPalID = PalIDsBG.palIDs[pa];
					for (int pi = 0; pi < 3; pi++)
					{
						Palette[palnum][0x10 + pa, 1+pi] = PalCollection[fgPalID * 3 + pi];
						Palette[palnum][0x00 + pa, 1+pi] = PalCollection[bgPalID * 3 + pi];
						if (BmpPalColLUT[fgPalID * 3 + pi] == 0)
							BmpPalColLUT[fgPalID * 3 + pi] = (byte)(1 + (0x10 + pa) * 3 + pi);
						if (BmpPalColLUT[bgPalID * 3 + pi] == 0)
							BmpPalColLUT[bgPalID * 3 + pi] = (byte)(1 + (0x00 + pa) * 3 + pi);
					}
				}
			}
			CurPal = 0;
			switch (Level.ObjectFormat)
			{
				/*case EngineVersion.S1:
					ObjectFormat = new S1.Object();
					break;
				case EngineVersion.S2:
				case EngineVersion.S2NA:
					ObjectFormat = new S2.Object();
					break;
				case EngineVersion.S3K:
				case EngineVersion.SKC:
					ObjectFormat = new S3K.Object();
					break;
				case EngineVersion.SCD:
				case EngineVersion.SCDPC:
					ObjectFormat = new SCD.Object();
					break;
				case EngineVersion.Chaotix:
					ObjectFormat = new Chaotix.Object();
					break;*/
				case EngineVersion.SPA:
					ObjectFormat = new SPA.Object();
					break;
				case EngineVersion.Custom:
					ObjectFormat = CompileCodeFile<ObjectLayoutFormat>(Level.ObjectCodeFile, Level.ObjectCodeType);
					break;
			}
			/*switch (Level.RingFormat)
			{
				case EngineVersion.S1:
				case EngineVersion.SCD:
				case EngineVersion.SCDPC:
					RingFormat = new S1.Ring();
					break;
				case EngineVersion.S2:
				case EngineVersion.S2NA:
					RingFormat = new S2.Ring();
					break;
				case EngineVersion.S3K:
				case EngineVersion.SKC:
					RingFormat = new S3K.Ring();
					break;
				case EngineVersion.Custom:
					RingFormat = CompileCodeFile<RingFormat>(Level.RingCodeFile, Level.RingCodeType);
					break;
			}*/
			RingFormat = new RingBGFormat();
			if (Level.Sprites != null)
			{
				Log("Loading 8x8 sprite tiles from file \"" + Level.Sprites + "\", using compression " + Level.TileCompression.ToString() + "...");
				SpriteTiles = Compression.Decompress(Level.Sprites, Level.TileCompression);
			}
			Sprites = new List<Sprite>();
			if (loadGraphics)
			{
				Bitmap palbmp = new Bitmap(1, 1, PixelFormat.Format8bppIndexed);
				BmpPal = palbmp.Palette;
				BmpPal.Entries[0] = PaletteToColor(0, 0, true);
				for (int i = 0; i < 32*3; i++)
					BmpPal.Entries[1+i] = PaletteToColor(i / 3, 1 + i % 3, true);
				BmpPalColMax = 1 + 32 * 3;
				BmpPal.Entries.Fill(Color.Black, 97, ColorFirstGlobal - 97);
				BmpPal.Entries[ColorRed] = Color.Red;
				BmpPal.Entries[ColorWhite] = Color.White;
				BmpPal.Entries[ColorYellow] = Color.Yellow;
				BmpPal.Entries[ColorBlack] = Color.Black;
				UnknownImg.Palette = BmpPal;
				/*if (Level.Sprites != null)
				{
					tmp = Compression.Decompress(Level.Sprites, CompressionType.SZDD);
					int numspr = ByteConverter.ToInt32(tmp, 8);
					int taddr = ByteConverter.ToInt32(tmp, 0xC);
					for (int i = 0; i < numspr; i++)
					{
						ushort width = ByteConverter.ToUInt16(tmp, 0x10 + (i * 0xC) + 4);
						if ((width & 4) == 4)
							width += 4;
						ushort height = ByteConverter.ToUInt16(tmp, 0x10 + (i * 0xC) + 6);
						ushort startcol = (ushort)(ByteConverter.ToUInt16(tmp, 0x10 + (i * 0xC) + 8) - 0x10);
						BitmapBits bmp = new BitmapBits(width, height);
						byte[] til = new byte[height * (width / 2)];
						Array.Copy(tmp, taddr, til, 0, til.Length);
						taddr += til.Length;
						LoadBitmap4BppIndexed(bmp, til, width / 2);
						bmp.IncrementIndexes(startcol);
						Sprites.Add(new Sprite(bmp, new Point(ByteConverter.ToInt16(tmp, 0x10 + (i * 0xC) + 0), ByteConverter.ToInt16(tmp, 0x10 + (i * 0xC) + 2))));
					}
				}*/
				INIObjDefs = new Dictionary<string, ObjectData>();
				ObjTypes = new Dictionary<byte, ObjectDefinition>();
				filecache = new Dictionary<string, byte[]>();
				unkobj = new DefaultObjectDefinition();
				unkobj.Init(new ObjectData());
				if (!System.IO.Directory.Exists("dllcache"))
				{
					System.IO.DirectoryInfo dir = System.IO.Directory.CreateDirectory("dllcache");
					dir.Attributes |= System.IO.FileAttributes.Hidden;
				}
				if (Game.ObjectList != null)
					foreach (string item in Game.ObjectList)
						LoadObjectDefinitionFile(item);
				if (Level.ObjectList != null)
					foreach (string item in Level.ObjectList)
						LoadObjectDefinitionFile(item);
				InitObjectDefinitions();
			}
			if (Level.Objects != null)
			{
				Objects = ObjectFormat.TryReadLayout(Level.Objects, Level.ObjectCompression, out objectterm);
				if (loadGraphics)
					for (int i = 0; i < Objects.Count; i++)
						Objects[i].UpdateSprite();
			}
			else
				Objects = new List<ObjectEntry>();
			/*if (Level.Rings != null && RingFormat is RingLayoutFormat)
			{
				Rings = ((RingLayoutFormat)RingFormat).TryReadLayout(Level.Rings, Level.RingCompression, out ringstartterm, out ringendterm);
				if (loadGraphics)
					foreach (RingEntry ring in Rings)
						ring.UpdateSprite();
			}
			else
				Rings = new List<RingEntry>();
			if (Level.Bumpers != null)
			{
				Bumpers = new List<CNZBumperEntry>();
				if (File.Exists(Level.Bumpers))
				{
					Log("Loading bumpers from file \"" + Level.Bumpers + "\", using compression " + Level.BumperCompression + "...");
					tmp = Compression.Decompress(Level.Bumpers, Level.BumperCompression);
					for (int i = 0; i < tmp.Length; i += CNZBumperEntry.Size)
					{
						if (ByteConverter.ToUInt16(tmp, i + 2) == 0xFFFF) break;
						CNZBumperEntry ent = new CNZBumperEntry(tmp, i);
						Bumpers.Add(ent);
						if (loadGraphics) ent.UpdateSprite();
					}
				}
				else
					Log("Bumper file \"" + Level.Bumpers + "\" not found.");
			}
			else
			{
				Bumpers = null;
			}*/
			StartPositions = new List<StartPositionEntry>();
			StartPosDefs = new List<StartPositionDefinition>();
			if (Level.StartPositions != null)
			{
				foreach (StartPositionInfo item in Level.StartPositions)
				{
					StartPositionEntry ent;
					if (File.Exists(item.Filename))
					{
						Log("Loading start position \"" + item.Name + "\" from file \"" + item.Filename + "\"...");
						ent = new StartPositionEntry(File.ReadAllBytes(item.Filename), item.Offset == -1 ? 0 : item.Offset);
					}
					else
					{
						Log("Start position file \"" + item.Filename + "\" not found.");
						ent = new StartPositionEntry();
					}
					StartPositions.Add(ent);
					if (loadGraphics)
					{
						if (!string.IsNullOrEmpty(item.Sprite))
							StartPosDefs.Add(new StartPositionDefinition(INIObjDefs[item.Sprite], item.Name));
						else
							StartPosDefs.Add(new StartPositionDefinition(item.Name));
						ent.UpdateSprite();
					}
				}
			}
			ColInds = new List<short>();
			tmp = Compression.Decompress(Level.CollisionIndex, Level.CollisionIndexCompression);
			for (int i = 0; i < tmp.Length; i += 2)
			{
				short colVal = ByteConverter.ToInt16(tmp, i);
				ColInds.Add(colVal);
			}
			if (ColInds.Count == Tiles.Count)
			{
				// In the vanilla game, there are as many tiles as there are collision
				// indices - INCLUDING the terminator (which has an empty tile).
				// So let's check for this case and remove the last tile.
				int lastIdx = ColInds.Count - 1;
				if (ColInds[lastIdx] == -1)
				{
					ushort tdused = 0;
					for (int i = 0; i < 16; i++)
						tdused |= Tiles[lastIdx][i];
					if (tdused == 0)
					{
						Tiles.RemoveAt(lastIdx);
						ColInds.RemoveAt(lastIdx);
					}
				}
				else
				{
					ColInds[lastIdx] = 0;
				}
			}
			if (ColInds.Count < Tiles.Count)
				ColInds.AddRange(new short[Tiles.Count - ColInds.Count]);
			/*ColInds1 = new List<byte>();
			ColInds2 = new List<byte>();
			switch (Level.CollisionIndexFormat)
			{
				case EngineVersion.S1:
				case EngineVersion.SCD:
				case EngineVersion.SCDPC:
				case EngineVersion.SPA:
					if (Level.CollisionIndex != null && File.Exists(Level.CollisionIndex))
						ColInds1.AddRange(Compression.Decompress(Level.CollisionIndex, Level.CollisionIndexCompression));
					ColInds2 = ColInds1;
					break;
				case EngineVersion.S2:
				case EngineVersion.S2NA:
					if (Level.CollisionIndex1 != null && File.Exists(Level.CollisionIndex1))
						ColInds1.AddRange(Compression.Decompress(Level.CollisionIndex1, Level.CollisionIndexCompression));
					if (Level.CollisionIndex2 != null)
					{
						if (File.Exists(Level.CollisionIndex2))
							ColInds2.AddRange(Compression.Decompress(Level.CollisionIndex2, Level.CollisionIndexCompression));
					}
					else
						ColInds2 = ColInds1;
					break;
				case EngineVersion.S3K:
				case EngineVersion.SKC:
					if (Level.CollisionIndex != null && File.Exists(Level.CollisionIndex))
					{
						tmp = Compression.Decompress(Level.CollisionIndex, Level.CollisionIndexCompression);
						switch (Level.CollisionIndexSize)
						{
							case 0:
							case 1:
								Array.Resize(ref tmp, 0x600);
								for (int i = 0; i < 0x600; i += 2)
								{
									ColInds1.Add(tmp[i]);
									ColInds2.Add(tmp[i + 1]);
								}
								break;
							case 2:
								for (int i = 0; i < 0x600; i += 2)
									ColInds1.Add((byte)ByteConverter.ToUInt16(tmp, i));
								for (int i = 0x600; i < 0xC00; i += 2)
									ColInds2.Add((byte)ByteConverter.ToUInt16(tmp, i));
								break;
						}
					}
					else
					{
						ColInds1.AddRange(new byte[0x300]);
						ColInds2.AddRange(new byte[0x300]);
					}
					break;
			}
			if (Level.EngineVersion != EngineVersion.S3K && Level.EngineVersion != EngineVersion.SKC)
			{
				if (ColInds1.Count < Blocks.Count)
					ColInds1.AddRange(new byte[Blocks.Count - ColInds1.Count]);
				if (ColInds2.Count < Blocks.Count)
					ColInds2.AddRange(new byte[Blocks.Count - ColInds2.Count]);
			}
			ColArr1 = new sbyte[256][];
			if (Level.CollisionArray1 != null && File.Exists(Level.CollisionArray1))
				tmp = Compression.Decompress(Level.CollisionArray1, Level.CollisionArrayCompression);
			else
				tmp = new byte[256 * 16];
			for (int i = 0; i < 256; i++)
			{
				ColArr1[i] = new sbyte[16];
				for (int j = 0; j < 16; j++)
					ColArr1[i][j] = unchecked((sbyte)tmp[(i * 16) + j]);
			}
			if (Level.Angles != null && File.Exists(Level.Angles))
				Angles = Compression.Decompress(Level.Angles, Level.AngleCompression);
			else
				Angles = new byte[256];*/
			if (loadGraphics)
			{
				BlockBmps = new List<Bitmap[]>();
				BlockBmpsA = new List<Bitmap>[2];
				BlockBmpsA[0] = new List<Bitmap>();
				BlockBmpsA[1] = new List<Bitmap>();
				BlockBmpBits = new List<BitmapBits[]>();
				BlockColBmps = new List<Bitmap>();
				BlockColBmpBits = new List<BitmapBits>();
				//CompBlockBmps = new List<Bitmap>();
				//CompBlockBmpBits = new List<BitmapBits>();
				LoadCollisionBitmap(Game.CollisionBitmap);
				Log("Drawing block bitmaps...");
				for (int bi = 0; bi < Blocks.Count; bi++)
				{
					BlockBmps.Add(new Bitmap[2]);
					BlockBmpBits.Add(new BitmapBits[2]);
					BlockColBmps.Add(null);
					BlockColBmpBits.Add(null);
					BlockBmpsA[0].Add(null);
					BlockBmpsA[1].Add(null);
					//CompBlockBmps.Add(null);
					//CompBlockBmpBits.Add(null);
					RedrawBlock(bi, false);
				}
				/*ColBmps = new Bitmap[256];
				ColBmpBits = new BitmapBits[256];
				for (int ci = 0; ci < 256; ci++)
					RedrawCol(ci, false);
				ChunkBmps = new List<Bitmap[]>();
				ChunkBmpBits = new List<BitmapBits[]>();
				ChunkColBmps = new List<Bitmap[]>();
				ChunkColBmpBits = new List<BitmapBits[]>();
				CompChunkBmps = new List<Bitmap>();
				CompChunkBmpBits = new List<BitmapBits>();
				Log("Drawing chunk bitmaps...");
				for (int ci = 0; ci < Chunks.Count; ci++)
				{
					ChunkBmps.Add(new Bitmap[2]);
					ChunkBmpBits.Add(new BitmapBits[2]);
					ChunkColBmps.Add(new Bitmap[2]);
					ChunkColBmpBits.Add(new BitmapBits[2]);
					CompChunkBmps.Add(null);
					CompChunkBmpBits.Add(null);
					RedrawChunk(ci);
				}*/
			}
		}

		public static void LoadCollisionBitmap(string bmpFile)
		{
			Bitmap collBmp = new Bitmap(bmpFile);
			int tilesX, tilesY, totalTiles;
			int ti, tx, ty;

			tilesX = collBmp.Width / 8;
			tilesY = collBmp.Height / 8;
			totalTiles = tilesX * tilesY;
			ColBmps = new Bitmap[totalTiles + 1];
			ColBmpBits = new BitmapBits[totalTiles + 1];
			
			// the first ID is "no collision"
			ti = 0;
			ColBmpBits[ti] = new BitmapBits(8, 8);
			for (int py = 0; py < 8; py++)
				for (int px = 0; px < 8; px++)
					ColBmpBits[ti][px, py] = 0;
			ColBmps[ti] = ColBmpBits[ti].ToBitmap(Color.Transparent, Color.White, Color.LightGray);
			
			for (ti = 1; ti <= totalTiles; ti ++)
			{
				ty = (ti - 1) / tilesX * 8;
				tx = (ti - 1) % tilesX * 8;
				ColBmpBits[ti] = new BitmapBits(8, 8);
				for (int py = 0; py < 8; py++)
				{
					for (int px = 0; px < 8; px++)
					{
						Color pixCol = collBmp.GetPixel(tx + px, ty + py);
						int grey = (pixCol.R + pixCol.G + pixCol.B) / 3;
						byte collCol;

						if (grey < 0xA0)
							collCol = 0;	// no collision
						else if (grey < 0xE0)
							collCol = 2;	// graphical helper
						else
							collCol = 1;	// collision
						ColBmpBits[ti][px, py] = collCol;
					}
				}
				ColBmps[ti] = ColBmpBits[ti].ToBitmap(Color.Transparent, Color.White, Color.LightGray);
			}
		}

		private static T CompileCodeFile<T>(string codefile, string typename)
		{
			string dllfile = System.IO.Path.Combine("dllcache", typename + ".dll");
			DateTime modDate = DateTime.MinValue;
			if (System.IO.File.Exists(dllfile))
				modDate = System.IO.File.GetLastWriteTime(dllfile);
			string fp = codefile.Replace('/', System.IO.Path.DirectorySeparatorChar);
			Log("Loading type " + typename + " from \"" + fp + "\"...");
			if (modDate >= File.GetLastWriteTime(fp) & modDate > File.GetLastWriteTime(Application.ExecutablePath))
			{
				Log("Loading type from cached assembly \"" + dllfile + "\"...");
				return (T)Activator.CreateInstance(System.Reflection.Assembly.LoadFile(Path.Combine(Environment.CurrentDirectory, dllfile)).GetType(typename));
			}
			else
			{
				Log("Compiling code file...");
				string ext = System.IO.Path.GetExtension(fp);
				CodeDomProvider pr = null;
				switch (ext.ToLowerInvariant())
				{
					case ".cs":
						pr = new Microsoft.CSharp.CSharpCodeProvider(new Dictionary<string, string>() { { "CompilerVersion", "v3.5" } });
						break;
					case ".vb":
						pr = new Microsoft.VisualBasic.VBCodeProvider(new Dictionary<string, string>() { { "CompilerVersion", "v3.5" } });
						break;
#if false
									case ".js":
										pr = new Microsoft.JScript.JScriptCodeProvider();
										break;
#endif
				}
				CompilerParameters para = new CompilerParameters(new string[] { "System.dll", "System.Core.dll", "System.Drawing.dll", System.Reflection.Assembly.GetExecutingAssembly().Location });
				para.GenerateExecutable = false;
				para.GenerateInMemory = false;
				para.IncludeDebugInformation = true;
				para.OutputAssembly = System.IO.Path.Combine(Environment.CurrentDirectory, dllfile);
				CompilerResults res = pr.CompileAssemblyFromFile(para, fp);
				if (res.Errors.HasErrors)
				{
					Log("Compile failed.", "Errors:");
					foreach (CompilerError item in res.Errors)
						Log(item.ToString());
					Log(string.Empty);
					throw new Exception("Failed compiling file.");
				}
				else
				{
					Log("Compile succeeded.");
					return (T)Activator.CreateInstance(res.CompiledAssembly.GetType(typename));
				}
			}
		}

		public static void SaveLevel()
		{
			Log("Saving " + Level.DisplayName + "...");
			int fileind = -1;
			List<byte> tmp;
			int addTempTile = -1;
			if (Tiles.Count == 0x1BF)
			{
				// If there are 0x1C0 collision indices, then simulate the vanilla game
				// and temporarily add an empty tile for the terminating collision index.
				byte[] emptyTile = new byte[16];
				emptyTile.FastFill(0);
				addTempTile = Tiles.Count;
				Tiles.Add(emptyTile);
			}
			ReadOnlyCollection<ReadOnlyCollection<byte[]>> tilefiles = Tiles.GetFiles();
			//if (Level.TileFormat != EngineVersion.SCDPC)
			{
				foreach (FileInfo tileent in Level.Tiles)
				{
					fileind++;
					tmp = new List<byte>();
					foreach (byte[] item in tilefiles[fileind])
						tmp.AddRange(item);
					Compression.Compress(tmp.ToArray(), tileent.Filename, Level.TileCompression);
				}
			}
			/*else
			{
				List<ushort>[] tilepals = new List<ushort>[4];
				for (int i = 0; i < 4; i++)
					tilepals[i] = new List<ushort>();
				foreach (Block blk in Blocks)
					for (int y = 0; y < 2; y++)
						for (int x = 0; x < 2; x++)
							if (!tilepals[blk.Tiles[x, y].Palette].Contains(blk.Tiles[x, y].Tile))
								tilepals[blk.Tiles[x, y].Palette].Add(blk.Tiles[x, y].Tile);
				foreach (Block blk in Blocks)
					for (int y = 0; y < 2; y++)
						for (int x = 0; x < 2; x++)
						{
							byte pal = blk.Tiles[x, y].Palette;
							int c = 0;
							for (int i = pal - 1; i >= 0; i--)
								c += tilepals[i].Count;
							blk.Tiles[x, y].Tile = (ushort)(tilepals[pal].IndexOf(blk.Tiles[x, y].Tile) + c);
						}
				List<byte[]> tiles = new List<byte[]>();
				for (int p = 0; p < 4; p++)
					foreach (ushort item in tilepals[p])
						if (Tiles[item] != null)
							tiles.Add(Tiles[item]);
						else
							tiles.Add(new byte[32]);
				Tiles.Clear();
				Tiles.AddFile(tiles, -1);
				UpdateTileArray();
				tmp = new List<byte>();
				tmp.Add(0x53);
				tmp.Add(0x43);
				tmp.Add(0x52);
				tmp.Add(0x4C);
				tmp.AddRange(ByteConverter.GetBytes(0x18 + (Tiles.Count * 4) + (Tiles.Count * 32)));
				tmp.AddRange(ByteConverter.GetBytes(Tiles.Count));
				tmp.AddRange(ByteConverter.GetBytes(0x18 + (Tiles.Count * 4)));
				for (int i = 0; i < 4; i++)
					tmp.AddRange(ByteConverter.GetBytes((ushort)tilepals[i].Count));
				for (int i = 0; i < Tiles.Count; i++)
				{
					tmp.AddRange(ByteConverter.GetBytes((ushort)8));
					tmp.AddRange(ByteConverter.GetBytes((ushort)8));
				}
				tmp.AddRange(TileArray);
				Compression.Compress(tmp.ToArray(), Level.Tiles[0].Filename, CompressionType.SZDD);
			}*/
			if (addTempTile >= 0)
				Tiles.RemoveAt(addTempTile);
			fileind = -1;
			ReadOnlyCollection<ReadOnlyCollection<Block>> blockfiles = Blocks.GetFiles();
			foreach (FileInfo tileent in Level.Blocks)
			{
				fileind++;
				tmp = new List<byte>();
				if (Level.EngineVersion == EngineVersion.SKC)
					littleendian = false;
				foreach (Block b in blockfiles[fileind])
					tmp.AddRange(b.GetBytes());
				if (Level.EngineVersion == EngineVersion.SKC)
					littleendian = true;
				Compression.Compress(tmp.ToArray(), tileent.Filename, Level.BlockCompression);
			}
			/*fileind = -1;
			ReadOnlyCollection<ReadOnlyCollection<Chunk>> chunkfiles = Chunks.GetFiles();
			foreach (FileInfo tileent in Level.Chunks)
			{
				fileind++;
				tmp = new List<byte>();
				if (Level.EngineVersion == EngineVersion.SKC)
					littleendian = false;
				foreach (Chunk c in chunkfiles[fileind])
					tmp.AddRange(c.GetBytes());
				if (Level.EngineVersion == EngineVersion.SKC)
					littleendian = true;
				if (fileind == 0)
					switch (Level.ChunkFormat)
					{
						case EngineVersion.S1:
						case EngineVersion.SCD:
						case EngineVersion.SCDPC:
							tmp.RemoveRange(0, Chunk.Size);
							break;
					}
				Compression.Compress(tmp.ToArray(), tileent.Filename, Level.ChunkCompression);
			}*/
			if (LayoutFormat.IsCombinedLayout)
			{
				LayoutFormatCombined lfc = (LayoutFormatCombined)LayoutFormat;
				lfc.WriteLayout(Layout, Level.LayoutCompression, Level.Layout);
				foreach (var item in AdditionalLayouts)
					lfc.WriteLayout(item.Value.Value, item.Value.Key, item.Key);
			}
			else
			{
				LayoutFormatSeparate lfs = (LayoutFormatSeparate)LayoutFormat;
				lfs.WriteLayout(Layout, Level.FGLayoutCompression, Level.BGLayoutCompression, Level.FGLayout, Level.BGLayout);
				foreach (var item in AdditionalLayouts)
					if (item.Value.Value.FGLayout != null)
						lfs.WriteFG(item.Value.Value, item.Value.Key, item.Key);
					else
						lfs.WriteBG(item.Value.Value, item.Value.Key, item.Key);
			}
			if (Level.PaletteFormat == EngineVersion.SPA)
			{
				for (int palnum = 0; palnum < Level.Palettes.Length; palnum++)
				{
					// done by SetPalCol
					/*for (int pa = 0; pa < 16; pa++)
					{
						ushort fgPalID = PalIDsFG.palIDs[pa];
						ushort bgPalID = PalIDsBG.palIDs[pa];
						for (int pi = 0; pi < 3; pi++)
						{
							PalCollection[fgPalID * 3 + pi] = Palette[palnum][0x10 + pa, 1+pi];
							PalCollection[bgPalID * 3 + pi] = Palette[palnum][0x00 + pa, 1+pi];
						}
					}*/
					tmp = new List<byte>();
					for (int pa = 0; pa < PalCollection.Length; pa ++)
						tmp.AddRange(ByteConverter.GetBytes(PalCollection[pa].NGPCColor));
					File.WriteAllBytes(Level.GamePalettes, tmp.ToArray());
				}
			}
			/*if (Level.PaletteFormat != EngineVersion.SCDPC)
			{
				byte[] paltmp;
				List<ushort[]> palfiles = new List<ushort[]>();
				byte palfilenum = 0;
				for (int palnum = 0; palnum < Level.Palettes.Length; palnum++)
				{
					PaletteList palent = Level.Palettes[palnum].Palettes;
					for (byte pn = 0; pn < palent.Collection.Length; pn++)
					{
						paltmp = File.ReadAllBytes(palent[pn].Filename);
						ushort[] palfile = new ushort[paltmp.Length / 2];
						for (int pi = 0; pi < paltmp.Length; pi += 2)
							palfile[pi / 2] = ByteConverter.ToUInt16(paltmp, pi);
						palfiles.Add(palfile);
					}
					for (int pl = 0; pl < 4; pl++)
						for (int pi = 0; pi < 16; pi++)
							palfiles[PalNum[palnum][pl, pi]][PalAddr[palnum][pl, pi]] = Palette[palnum][pl, pi].NGPCColor;
					for (byte pn = 0; pn < palent.Collection.Length; pn++)
					{
						tmp = new List<byte>();
						for (int pi = 0; pi < palfiles[pn + palfilenum].Length; pi++)
							tmp.AddRange(ByteConverter.GetBytes(palfiles[pn + palfilenum][pi]));
						File.WriteAllBytes(palent[pn].Filename, tmp.ToArray());
					}
					palfilenum = (byte)palfiles.Count;
				}
			}
			else
			{
				List<byte[]> palfiles = new List<byte[]>();
				byte palfilenum = 0;
				if (Level.Palettes.Length > 0)
				{
					PaletteList palent = Level.Palettes[0].Palettes;
					for (byte pn = 0; pn < palent.Collection.Length; pn++)
						palfiles.Add(System.IO.File.ReadAllBytes(palent[pn].Filename));
					for (int pl = 0; pl < 4; pl++)
					{
						for (int pi = 0; pi < 16; pi++)
						{
							palfiles[PalNum[0][pl, pi]][PalAddr[0][pl, pi] * 4] = Palette[0][pl, pi].R;
							palfiles[PalNum[0][pl, pi]][PalAddr[0][pl, pi] * 4 + 1] = Palette[0][pl, pi].G;
							palfiles[PalNum[0][pl, pi]][PalAddr[0][pl, pi] * 4 + 2] = Palette[0][pl, pi].B;
						}
					}
					for (byte pn = 0; pn < palent.Collection.Length; pn++)
						File.WriteAllBytes(palent[pn].Filename, palfiles[pn]);
					palfilenum = (byte)palfiles.Count;
				}
			}*/
			if (Level.Objects != null)
			{
				if (Level.ObjectFormat != EngineVersion.SPA)
					Objects.Sort();
				if (Level.ObjectFormat == EngineVersion.SPA)
					((SPA.Object)ObjectFormat).LayoutSize = new Size(Level.LevelWidth * 32, Level.LevelHeight * 32);
				ObjectFormat.WriteLayout(Objects, Level.ObjectCompression, Level.Objects, objectterm);
			}
			/*if (Level.Rings != null && RingFormat is RingLayoutFormat)
			{
				Rings.Sort();
				((RingLayoutFormat)RingFormat).WriteLayout(Rings, Level.RingCompression, Level.Rings, ringstartterm, ringendterm);
			}
			if (Bumpers != null)
			{
				Bumpers.Sort();
				tmp = new List<byte>();
				foreach (CNZBumperEntry item in Bumpers)
					tmp.AddRange(item.GetBytes());
				tmp.AddRange(new byte[] { 0, 0, 0xFF, 0xFF, 0, 0 });
				Compression.Compress(tmp.ToArray(), Level.Bumpers, Level.BumperCompression);
			}*/
			if (Level.StartPositions != null)
			{
				int i = 0;
				foreach (StartPositionInfo item in Level.StartPositions)
				{
					byte[] fc = new byte[4];
					if (File.Exists(item.Filename))
						fc = File.ReadAllBytes(item.Filename);
					StartPositions[i++].GetBytes().CopyTo(fc, item.Offset == -1 ? 0 : item.Offset);
					File.WriteAllBytes(item.Filename, fc);
				}
			}
			
			tmp = new List<byte>();
			for (int ci = 0; ci < ColInds.Count; ci ++)
				tmp.AddRange(ByteConverter.GetBytes(ColInds[ci]));
			tmp.AddRange(ByteConverter.GetBytes((short)-1));
			Compression.Compress(tmp.ToArray(), Level.CollisionIndex, Level.CollisionIndexCompression);
			/*switch (Level.CollisionIndexFormat)
			{
				case EngineVersion.S1:
				case EngineVersion.SCD:
				case EngineVersion.SCDPC:
					if (Level.CollisionIndex != null)
						Compression.Compress(ColInds1.ToArray(), Level.CollisionIndex, Level.CollisionIndexCompression);
					break;
				case EngineVersion.S2:
				case EngineVersion.S2NA:
					if (Level.CollisionIndex1 != null)
						Compression.Compress(ColInds1.ToArray(), Level.CollisionIndex1, Level.CollisionIndexCompression);
					if (Level.CollisionIndex2 != null)
						Compression.Compress(ColInds2.ToArray(), Level.CollisionIndex2, Level.CollisionIndexCompression);
					break;
				case EngineVersion.S3K:
				case EngineVersion.SKC:
					if (Level.CollisionIndex != null)
					{
						tmp = new List<byte>();
						byte[] cif = null;
						switch (Level.CollisionIndexSize)
						{
							case 0:
							case 1:
								cif = new byte[0x600];
								for (int i = 0; i < ColInds1.Count; i++)
								{
									tmp.Add(ColInds1[i]);
									tmp.Add(ColInds2[i]);
								}
								tmp.CopyTo(0, cif, 0, Math.Min(tmp.Count, cif.Length));
								break;
							case 2:
								cif = new byte[0xC00];
								foreach (byte item in ColInds1)
									tmp.AddRange(ByteConverter.GetBytes((ushort)item));
								tmp.CopyTo(0, cif, 0, Math.Min(tmp.Count, 0x600));
								tmp.Clear();
								foreach (byte item in ColInds2)
									tmp.AddRange(ByteConverter.GetBytes((ushort)item));
								tmp.CopyTo(0, cif, 0x600, Math.Min(tmp.Count, 0x600));
								break;
						}
						Compression.Compress(cif, Level.CollisionIndex, Level.CollisionIndexCompression);
					}
					break;
			}
			if (Level.CollisionArray1 != null)
			{
				tmp = new List<byte>();
				for (int i = 0; i < 256; i++)
					for (int j = 0; j < 16; j++)
						tmp.Add(unchecked((byte)ColArr1[i][j]));
				Compression.Compress(tmp.ToArray(), Level.CollisionArray1, Level.CollisionArrayCompression);
			}
			if (Level.CollisionArray2 != null)
			{
				sbyte[][] rotcol = GenerateRotatedCollision();
				tmp = new List<byte>();
				for (int i = 0; i < 256; i++)
					for (int j = 0; j < 16; j++)
						tmp.Add(unchecked((byte)rotcol[i][j]));
				Compression.Compress(tmp.ToArray(), Level.CollisionArray2, Level.CollisionArrayCompression);
			}
			if (Level.Angles != null)
				Compression.Compress(Angles, Level.Angles, Level.AngleCompression);*/
		}

		public static BitmapBits DrawForeground(Rectangle? section, bool includeObjects, bool includeDebugObjects, bool objectsAboveHighPlane, bool lowPlane, bool highPlane, bool collisionPath1, bool collisionPath2, bool allTimeZones)
		{
			Rectangle bounds;
			if (section.HasValue)
				bounds = section.Value;
			else
			{
				int xend = 0;
				int yend = 0;
				for (int y = 0; y < FGHeight; y++)
					for (int x = 0; x < FGWidth; x++)
					{
						if (Layout.BGLayout[x, y] > 0)
						{
							xend = Math.Max(xend, x);
							yend = Math.Max(yend, y);
						}
						if (Layout.FGLayout[x, y] > 0)
						{
							xend = Math.Max(xend, x);
							yend = Math.Max(yend, y);
						}
					}
				xend++;
				yend++;
				bounds = new Rectangle(0, 0, xend * 32, yend * 32);
			}
			BitmapBits LevelImg8bpp = new BitmapBits(bounds.Size);
			for (int y = Math.Max(bounds.Y / 32, 0); y <= Math.Min((bounds.Bottom - 1) / 32, FGHeight - 1); y++)
				for (int x = Math.Max(bounds.X / 32, 0); x <= Math.Min((bounds.Right - 1) / 32, FGWidth - 1); x++)
				{
					if (Layout.BGLayout[x, y] < Blocks.Count)
					{
						if (lowPlane)
							LevelImg8bpp.DrawBitmapComposited(BlockBmpBits[Layout.BGLayout[x, y]][0], x * 32 - bounds.X, y * 32 - bounds.Y);
						if (collisionPath1 && ! objectsAboveHighPlane)
							LevelImg8bpp.DrawBitmapComposited(BlockColBmpBits[Layout.BGLayout[x, y]], x * 32 - bounds.X, y * 32 - bounds.Y);
					}
					if (objectsAboveHighPlane)
					{
						if (Layout.FGLayout[x, y] < Blocks.Count && highPlane)
							LevelImg8bpp.DrawBitmapComposited(BlockBmpBits[Layout.FGLayout[x, y]][1], x * 32 - bounds.X, y * 32 - bounds.Y);
						if (Layout.BGLayout[x, y] < Blocks.Count && collisionPath1)
							LevelImg8bpp.DrawBitmapComposited(BlockColBmpBits[Layout.BGLayout[x, y]], x * 32 - bounds.X, y * 32 - bounds.Y);
						if (Layout.FGLayout[x, y] < Blocks.Count && collisionPath2)
							LevelImg8bpp.DrawBitmapComposited(BlockColBmpBits[Layout.FGLayout[x, y]], x * 32 - bounds.X, y * 32 - bounds.Y);
					}
				}
			// --- end of DrawBackground
			if (includeObjects)
			{
				for (int oi = 0; oi < Objects.Count; oi++)
				{
					ObjectEntry oe = Objects[oi];
					if (!(!includeDebugObjects && GetObjectDefinition(oe.ID).Debug) && ObjectVisible(oe, allTimeZones))
						LevelImg8bpp.DrawSprite(oe.Sprite, -bounds.X, -bounds.Y);
				}
				/*if (Rings != null && RingFormat is RingLayoutFormat)
					for (int ri = 0; ri < Rings.Count; ri++)
						LevelImg8bpp.DrawSprite(Rings[ri].Sprite, -bounds.X, -bounds.Y);
				if (Bumpers != null && includeDebugObjects)
					foreach (CNZBumperEntry item in Bumpers)
						LevelImg8bpp.DrawSprite(item.Sprite, -bounds.X, -bounds.Y);*/
				foreach (StartPositionEntry item in StartPositions)
					LevelImg8bpp.DrawSprite(item.Sprite, -bounds.X, -bounds.Y);
			}
			if (!objectsAboveHighPlane)
				for (int y = Math.Max(bounds.Y / 32, 0); y <= Math.Min(bounds.Bottom / 32, Layout.FGLayout.GetLength(1) - 1); y++)
					for (int x = Math.Max(bounds.X / 32, 0); x <= Math.Min(bounds.Right / 32, Layout.FGLayout.GetLength(0) - 1); x++)
						if (Layout.FGLayout[x, y] < Blocks.Count)
						{
							if (highPlane)
								LevelImg8bpp.DrawBitmapComposited(BlockBmpBits[Layout.FGLayout[x, y]][1], x * 32 - bounds.X, y * 32 - bounds.Y);
							if (collisionPath2)
								LevelImg8bpp.DrawBitmapComposited(BlockColBmpBits[Layout.FGLayout[x, y]], x * 32 - bounds.X, y * 32 - bounds.Y);
						}
			return LevelImg8bpp;
		}

		public static BitmapBits DrawBackground(Rectangle? section, bool lowPlane, bool highPlane, bool collisionPath1, bool collisionPath2)
		{
			Rectangle bounds;
			if (section.HasValue)
				bounds = section.Value;
			else
			{
				int xend = 0;
				int yend = 0;
				for (int y = 0; y < Layout.BGLayout.GetLength(1); y++)
					for (int x = 0; x < Layout.BGLayout.GetLength(0); x++)
					{
						if (Layout.BGLayout[x, y] > 0)
						{
							xend = Math.Max(xend, x);
							yend = Math.Max(yend, y);
						}
						if (Layout.FGLayout[x, y] > 0)
						{
							xend = Math.Max(xend, x);
							yend = Math.Max(yend, y);
						}
					}
				xend++;
				yend++;
				bounds = new Rectangle(0, 0, xend * 32, yend * 32);
			}
			BitmapBits LevelImg8bpp = new BitmapBits(bounds.Size);
			for (int y = Math.Max(bounds.Y / 32, 0); y <= Math.Min((bounds.Bottom - 1) / 32, Layout.BGLayout.GetLength(1) - 1); y++)
				for (int x = Math.Max(bounds.X / 32, 0); x <= Math.Min((bounds.Right - 1) / 32, Layout.BGLayout.GetLength(0) - 1); x++)
					if (Layout.BGLayout[x, y] < Blocks.Count)
					{
						if (lowPlane)
							LevelImg8bpp.DrawBitmapComposited(BlockBmpBits[Layout.BGLayout[x, y]][0], x * 32 - bounds.X, y * 32 - bounds.Y);
						if (highPlane)
							LevelImg8bpp.DrawBitmapComposited(BlockBmpBits[Layout.FGLayout[x, y]][1], x * 32 - bounds.X, y * 32 - bounds.Y);
						if (collisionPath1)
							LevelImg8bpp.DrawBitmapComposited(BlockColBmpBits[Layout.BGLayout[x, y]], x * 32 - bounds.X, y * 32 - bounds.Y);
						else if (collisionPath2)
							LevelImg8bpp.DrawBitmapComposited(BlockColBmpBits[Layout.FGLayout[x, y]], x * 32 - bounds.X, y * 32 - bounds.Y);
					}
			return LevelImg8bpp;
		}

		public static bool ObjectVisible(ObjectEntry obj, bool allTimeZones)
		{
			if (allTimeZones)
				return true;
			/*if (obj is SonicRetro.SonLVL.API.SCD.SCDObjectEntry)
			{
				SonicRetro.SonLVL.API.SCD.SCDObjectEntry scdobj = (SonicRetro.SonLVL.API.SCD.SCDObjectEntry)obj;
				switch (Level.TimeZone)
				{
					case API.TimeZone.Past:
						return scdobj.ShowPast;
					case API.TimeZone.Present:
						return scdobj.ShowPresent;
					case API.TimeZone.Future:
						return scdobj.ShowFuture;
					default:
						return true;
				}
			}*/
			if (obj is SonicRetro.SonLVL.API.SPA.SPAObjectEntry)
			{
				SonicRetro.SonLVL.API.SPA.SPAObjectEntry spaobj = (SonicRetro.SonLVL.API.SPA.SPAObjectEntry)obj;
				if (!spaobj.Difficulty)
					return true;
				// return (! easyMode);
			}
			return true;
		}

		private static void LoadObjectDefinitionFile(string file)
		{
			Log("Loading object definition file \"" + file + "\".");
			Dictionary<string, ObjectData> obj = IniSerializer.Deserialize<Dictionary<string, ObjectData>>(file);
			foreach (KeyValuePair<string, ObjectData> group in obj)
			{
				group.Value.Init();
				INIObjDefs[group.Key] = group.Value;
			}
		}

		private static void InitObjectDefinitions()
		{
			foreach (KeyValuePair<string, ObjectData> group in INIObjDefs)
			{
				//if (group.Value.ArtCompression == CompressionType.Invalid)
				//	group.Value.ArtCompression = Game.ObjectArtCompression;
				byte ID;
				/*if (group.Key == "Ring" && RingFormat is RingLayoutFormat)
					((RingLayoutFormat)RingFormat).Init(group.Value);
				else*/ if (byte.TryParse(group.Key, System.Globalization.NumberStyles.HexNumber, System.Globalization.NumberFormatInfo.InvariantInfo, out ID))
				{
					ObjectDefinition def = null;
					if (group.Value.CodeFile != null)
					{
						string fulltypename = group.Value.CodeType;
						string dllfile = Path.Combine("dllcache", fulltypename + ".dll");
						DateTime modDate = DateTime.MinValue;
						if (File.Exists(dllfile))
							modDate = System.IO.File.GetLastWriteTime(dllfile);
						string fp = group.Value.CodeFile.Replace('/', Path.DirectorySeparatorChar);
						Log("Loading ObjectDefinition type " + fulltypename + " from \"" + fp + "\"...");
						if (modDate >= File.GetLastWriteTime(fp) & modDate > File.GetLastWriteTime(Application.ExecutablePath))
						{
							Log("Loading type from cached assembly \"" + dllfile + "\"...");
							def = (ObjectDefinition)Activator.CreateInstance(System.Reflection.Assembly.LoadFile(Path.Combine(Environment.CurrentDirectory, dllfile)).GetType(fulltypename));
						}
						else
						{
							Log("Compiling code file...");
							string ext = Path.GetExtension(fp);
							CodeDomProvider pr = null;
							switch (ext.ToLowerInvariant())
							{
								case ".cs":
									pr = new Microsoft.CSharp.CSharpCodeProvider(new Dictionary<string, string>() { { "CompilerVersion", "v3.5" } });
									break;
								case ".vb":
									pr = new Microsoft.VisualBasic.VBCodeProvider(new Dictionary<string, string>() { { "CompilerVersion", "v3.5" } });
									break;
#if false
								case ".js":
									pr = new Microsoft.JScript.JScriptCodeProvider();
									break;
#endif
							}
							if (pr != null)
							{
								CompilerParameters para = new CompilerParameters(new string[] { "System.dll", "System.Core.dll", "System.Drawing.dll", System.Reflection.Assembly.GetExecutingAssembly().Location });
								para.GenerateExecutable = false;
								para.GenerateInMemory = false;
								para.IncludeDebugInformation = true;
								para.OutputAssembly = Path.Combine(Environment.CurrentDirectory, dllfile);
								CompilerResults res = pr.CompileAssemblyFromFile(para, fp);
								if (res.Errors.HasErrors)
								{
									Log("Compile failed.", "Errors:");
									foreach (CompilerError item in res.Errors)
										Log(item.ToString());
									Log(string.Empty);
									def = new DefaultObjectDefinition();
								}
								else
								{
									Log("Compile succeeded.");
									def = (ObjectDefinition)Activator.CreateInstance(res.CompiledAssembly.GetType(fulltypename));
								}
							}
							else
								def = new DefaultObjectDefinition();
						}
					}
					else if (group.Value.XMLFile != null)
						def = new XMLObjectDefinition();
					else
						def = new DefaultObjectDefinition();
					ObjTypes[ID] = def;
					def.Init(group.Value);
				}
			}
		}

		internal static string ExpandTypeName(string type)
		{
			switch (type)
			{
				case "bool":
					return typeof(bool).FullName;
				case "byte":
					return typeof(byte).FullName;
				case "char":
					return typeof(char).FullName;
				case "decimal":
					return typeof(decimal).FullName;
				case "double":
					return typeof(double).FullName;
				case "float":
					return typeof(float).FullName;
				case "int":
					return typeof(int).FullName;
				case "long":
					return typeof(long).FullName;
				case "object":
					return typeof(object).FullName;
				case "sbyte":
					return typeof(sbyte).FullName;
				case "short":
					return typeof(short).FullName;
				case "string":
					return typeof(string).FullName;
				case "uint":
					return typeof(uint).FullName;
				case "ulong":
					return typeof(ulong).FullName;
				case "ushort":
					return typeof(ushort).FullName;
				default:
					return type;
			}
		}

		public static byte[] ReadFile(string file, CompressionType cmp)
		{
			if (file == "LevelArt")
				return TileArray;
			else if (filecache.ContainsKey(file))
				return filecache[file];
			else
			{
				byte[] val = Compression.Decompress(file, cmp);
				filecache.Add(file, val);
				return val;
			}
		}

		public static Bitmap TileToBmp4bpp(byte[] file, int index, int palette)
		{
			Bitmap bmp = new Bitmap(8, 8, PixelFormat.Format4bppIndexed);
			if (file != null && index * 16 + 16 <= file.Length)
			{
				// convert 2bpp images into 4bpp
				byte[] bmpdata = new byte[32];
				for (int i=0; i < 8; i ++)
				{
					ushort val = ByteConverter.ToUInt16(file, index * 16 + i * 2);
					byte pixval;
					for (int j = 7; j >= 0; j --, val >>= 2)
					{
						if ((val & 0x03) == 0)
							pixval = 0;
						else
							pixval = (byte)(val & 0x03);
						if ((j & 1) == 0)
							bmpdata[i * 4 + j / 2] |= (byte)(pixval << 4);
						else
							bmpdata[i * 4 + j / 2]  = (byte)(pixval << 0);
					}
				}
				BitmapData bmpd = bmp.LockBits(new Rectangle(0, 0, 8, 8), ImageLockMode.WriteOnly, PixelFormat.Format4bppIndexed);
				//System.Runtime.InteropServices.Marshal.Copy(file, index * 16, bmpd.Scan0, 32);
				System.Runtime.InteropServices.Marshal.Copy(bmpdata, 0, bmpd.Scan0, 32);
				bmp.UnlockBits(bmpd);
			}
			else
				InvalidTile.ToBitmap4bpp(bmp);
			ColorPalette pal = bmp.Palette;
			for (int i = 0; i < 4; i++)
				pal.Entries[i] = PaletteToColor(palette, i, false);
			bmp.Palette = pal;
			return bmp;
		}

		/*public static Bitmap InterlacedTileToBmp4bpp(byte[] file, int index, int palette)
		{
			Bitmap bmp = new Bitmap(8, 16, PixelFormat.Format4bppIndexed);
			if (file != null && index * 32 + 64 <= file.Length)
			{
				BitmapData bmpd = bmp.LockBits(new Rectangle(0, 0, 8, 16), ImageLockMode.WriteOnly, PixelFormat.Format4bppIndexed);
				System.Runtime.InteropServices.Marshal.Copy(file, index * 32, bmpd.Scan0, 64);
				bmp.UnlockBits(bmpd);
			}
			ColorPalette pal = bmp.Palette;
			pal.Entries[0] = PaletteToColor(palette, 0, false);
			for (int i = 0; i < 3; i++)
				pal.Entries[1+i] = PaletteToColor(palette, 1 + i, false);
			bmp.Palette = pal;
			return bmp;
		}*/

		public static BitmapBits TileToBmp8bpp(byte[] file, int index, int pal)
		{
			BitmapBits bmp;
			if (file != null && index * 16 + 16 <= file.Length)
			{
				bmp = new BitmapBits(8, 8);
				for (int i = 0; i < 8; i++)
				{
					ushort val = ByteConverter.ToUInt16(file, index * 16 + i * 2);
					for (int j = 7; j >= 0; j --, val >>= 2)
					{
						if ((val & 0x03) == 0)
							bmp.Bits[i * 8 + j] = 0;
						else
							bmp.Bits[i * 8 + j] = (byte)(pal * 3 + (val & 0x03));
					}
				}
			}
			else
			{
				bmp = new BitmapBits(InvalidTile);
				bmp.IncrementIndexes(pal * 3);
			}
			return bmp;
		}

		public static BitmapBits TileToBmp8bppLUT(byte[] file, int index, byte[] colorLUT)
		{
			BitmapBits bmp;
			if (file != null && index * 16 + 16 <= file.Length)
			{
				bmp = new BitmapBits(8, 8);
				for (int i = 0; i < 8; i++)
				{
					ushort val = ByteConverter.ToUInt16(file, index * 16 + i * 2);
					for (int j = 7; j >= 0; j--, val >>= 2)
					{
						if ((val & 0x03) == 0)
							bmp.Bits[i * 8 + j] = 0;
						else
							bmp.Bits[i * 8 + j] = colorLUT[val & 0x03];
					}
				}
			}
			else
			{
				bmp = new BitmapBits(InvalidTile);
				for (int i = 0; i < 8 * 8; i++)
				{
					if (bmp.Bits[i] != 0)
						bmp.Bits[i] = colorLUT[bmp.Bits[i]];
				}
			}
			return bmp;
		}

		/*public static BitmapBits InterlacedTileToBmp8bpp(byte[] file, int index, int pal)
		{
			BitmapBits bmp = new BitmapBits(8, 16);
			if (file != null && index * 32 + 64 <= file.Length)
				for (int i = 0; i < 64; i++)
				{
					bmp.Bits[i * 2] = (byte)((file[i + (index * 32)] >> 4) + (pal * 16));
					bmp.Bits[(i * 2) + 1] = (byte)((file[i + (index * 32)] & 0xF) + (pal * 16));
					if (bmp.Bits[i * 2] % 16 == 0) bmp.Bits[i * 2] = 0;
					if (bmp.Bits[(i * 2) + 1] % 16 == 0) bmp.Bits[(i * 2) + 1] = 0;
				}
			else if (file != null && index * 32 + 32 <= file.Length)
			{
				for (int i = 0; i < 32; i++)
				{
					bmp.Bits[i * 2] = (byte)((file[i + (index * 32)] >> 4) + (pal * 16));
					bmp.Bits[(i * 2) + 1] = (byte)((file[i + (index * 32)] & 0xF) + (pal * 16));
					if (bmp.Bits[i * 2] % 16 == 0) bmp.Bits[i * 2] = 0;
					if (bmp.Bits[(i * 2) + 1] % 16 == 0) bmp.Bits[(i * 2) + 1] = 0;
				}
				BitmapBits tmp = new BitmapBits(InvalidTile);
				tmp.IncrementIndexes(pal * 16);
				bmp.DrawBitmap(tmp, 0, 8);
			}
			else
			{
				BitmapBits tmp = new BitmapBits(InvalidTile);
				tmp.IncrementIndexes(pal * 16);
				bmp.DrawBitmap(tmp, 0, 0);
				bmp.DrawBitmap(tmp, 0, 8);
			}
			return bmp;
		}*/

		public static int[] GetOffsetList(byte[] file)
		{
			short endlist = short.MaxValue;
			List<int> addresses = new List<int>();
			for (int i = 0; i < endlist; i += 2)
			{
				short val = ByteConverter.ToInt16(file, i);
				if (val > -1)
				{
					addresses.Add(val);
					if (val > 0)
						endlist = Math.Min(endlist, val);
				}
			}
			return addresses.ToArray();
		}

		public static byte[] ASMToBin(string file, EngineVersion version) { Dictionary<string, int> labels; return ASMToBin(file, version, out labels); }

		public static byte[] ASMToBin(string file, EngineVersion version, out Dictionary<string, int> labels) { return ASMToBin(file, 0, version, out labels); }

		public static byte[] ASMToBin(string file, string label, EngineVersion version) { Dictionary<string, int> labels; return ASMToBin(file, label, version, out labels); }

		public static byte[] ASMToBin(string file, string label, EngineVersion version, out Dictionary<string, int> labels)
		{
			string[] fc = System.IO.File.ReadAllLines(file);
			int sti = -1;
			for (int i = 0; i < fc.Length; i++)
			{
				if (fc[i].StartsWith(label + ":"))
				{
					sti = i;
					fc[i] = fc[i].Substring(label.Length + 1);
				}
			}
			if (sti == -1)
			{
				labels = new Dictionary<string, int>();
				return new byte[0];
			}
			return ASMToBin(file, sti, version, out labels);
		}

		public static byte[] ASMToBin(string file, int sti, EngineVersion version) { Dictionary<string, int> labels; return ASMToBin(file, sti, version, out labels); }

		public static readonly Dictionary<string, int> asmlabels = new Dictionary<string, int>() {
			{ "afEnd", 0xFF }, // return to beginning of animation
			{ "afBack", 0xFE },// go back (specified number) bytes
			{ "afChange", 0xFD }, // run specified animation
			{ "afRoutine", 0xFC }, // increment routine counter
			{ "afReset", 0xFB }, // reset animation and 2nd object routine counter
			{ "af2ndRoutine", 0xFA } // increment 2nd routine counter
		};

		public static byte[] ASMToBin(string file, int sti, EngineVersion version, out Dictionary<string, int> labels)
		{
			labels = GetASMLabels(file, sti, version);
			foreach (KeyValuePair<string, int> item in asmlabels)
				if (!labels.ContainsKey(item.Key))
					labels.Add(item.Key, item.Value);
			string[] fc = System.IO.File.ReadAllLines(file);
			List<byte> result = new List<byte>();
			string lastlabel = string.Empty;
			string offsetLabel = string.Empty;
			for (int st = sti; st < fc.Length; st++)
			{
				string[] ln = fc[st].Split(';')[0].Trim().Split(new char[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
				if (ln.Length == 0) continue;
				if (!char.IsWhiteSpace(fc[st], 0))
				{
					string[] l = ln[0].Split(new char[] { ':' }, StringSplitOptions.RemoveEmptyEntries);
					lastlabel = l[0];
					if (l.Length == 1)
					{
						string[] ln2 = new string[ln.Length - 1];
						for (int i = 0; i < ln2.Length; i++)
							ln2[i] = ln[i + 1];
						ln = ln2;
					}
					else
						ln[0] = l[1];
					if (ln.Length == 0) continue;
				}
				if (ln[0].Equals("even"))
				{
					if (result.Count % 2 == 1)
						result.Add(0);
				}
				else if (ln[0].Equals("align"))
				{
					uint alignment = (uint)ParseASMNum(ln[1], labels);
					if (result.Count % alignment != 0)
						result.AddRange(new byte[alignment - (result.Count % alignment)]);
				}
				else if (ln[0].Equals("offsetTable") | ln[0].Equals("mappingsTable"))
					offsetLabel = lastlabel;
				else if (ln[0].StartsWith("offsetTableEntry.") | ln[0].StartsWith("mappingsTableEntry."))
					switch (ln[0].Split('.')[1])
					{
						case "b":
							result.Add(unchecked((byte)LabelSubtract(ln[1], offsetLabel, labels)));
							break;
						case "w":
							result.AddRange(ByteConverter.GetBytes((short)LabelSubtract(ln[1], offsetLabel, labels)));
							break;
						case "l":
							result.AddRange(ByteConverter.GetBytes(LabelSubtract(ln[1], offsetLabel, labels)));
							break;
					}
				else if (ln[0].StartsWith("dc."))
				{
					string d = string.Empty;
					for (int i = 1; i < ln.Length; i++)
						d += ln[i];
					string[] dats = d.Split(',');
					switch (ln[0].Split('.')[1])
					{
						case "b":
							foreach (string item in dats)
								if (!item.StartsWith("-") && item.Contains("-"))
									result.Add(unchecked((byte)ParseASMOffset(item, labels)));
								else
									result.Add((byte)ParseASMNum(item, labels));
							break;
						case "w":
							foreach (string item in dats)
								if (!item.StartsWith("-") && item.Contains("-"))
									result.AddRange(ByteConverter.GetBytes((short)ParseASMOffset(item, labels)));
								else
									result.AddRange(ByteConverter.GetBytes((ushort)ParseASMNum(item, labels)));
							break;
						case "l":
							foreach (string item in dats)
								if (!item.StartsWith("-") && item.Contains("-"))
									result.AddRange(ByteConverter.GetBytes(ParseASMOffset(item, labels)));
								else
									result.AddRange(ByteConverter.GetBytes((uint)ParseASMNum(item, labels)));
							break;
					}
				}
				else if (ln[0].Equals("spriteHeader"))
				{
					switch (version)
					{
						case EngineVersion.S1:
							result.Add(unchecked((byte)((LabelSubtract(lastlabel + "_End", lastlabel, labels) - 1) / 5)));
							break;
						case EngineVersion.S2:
							result.AddRange(ByteConverter.GetBytes((short)((LabelSubtract(lastlabel + "_End", lastlabel, labels) - 2) / 8)));
							break;
						case EngineVersion.S3K:
							result.AddRange(ByteConverter.GetBytes((short)((LabelSubtract(lastlabel + "_End", lastlabel, labels) - 2) / 6)));
							break;
					}
				}
				/*else if (ln[0].Equals("spritePiece"))
				{
					string d = string.Empty;
					for (int i = 1; i < ln.Length; i++)
						d += ln[i];
					string[] dats = d.Split(',');
					unchecked { result.AddRange(new MappingsTile((short)ParseASMNum(dats[0], labels), (short)ParseASMNum(dats[1], labels), (byte)ParseASMNum(dats[2], labels), (byte)ParseASMNum(dats[3], labels), (ushort)ParseASMNum(dats[4], labels), (byte)ParseASMNum(dats[5], labels) == 1, (byte)ParseASMNum(dats[6], labels) == 1, (byte)ParseASMNum(dats[7], labels), (byte)ParseASMNum(dats[8], labels) == 1).GetBytes(version)); }
				}
				else if (ln[0].Equals("spritePiece2P"))
				{
					string d = string.Empty;
					for (int i = 1; i < ln.Length; i++)
						d += ln[i];
					string[] dats = d.Split(',');
					unchecked { result.AddRange(new MappingsTile((short)ParseASMNum(dats[0], labels), (short)ParseASMNum(dats[1], labels), (byte)ParseASMNum(dats[2], labels), (byte)ParseASMNum(dats[3], labels), (ushort)ParseASMNum(dats[4], labels), (byte)ParseASMNum(dats[5], labels) == 1, (byte)ParseASMNum(dats[6], labels) == 1, (byte)ParseASMNum(dats[7], labels), (byte)ParseASMNum(dats[8], labels) == 1, (ushort)ParseASMNum(dats[9], labels), (byte)ParseASMNum(dats[10], labels) == 1, (byte)ParseASMNum(dats[11], labels) == 1, (byte)ParseASMNum(dats[12], labels), (byte)ParseASMNum(dats[13], labels) == 1).GetBytes(version)); }
				}
				else if (ln[0].Equals("dplcHeader"))
				{
					switch (version)
					{
						case EngineVersion.S1:
							result.Add(unchecked((byte)((LabelSubtract(lastlabel + "_End", lastlabel, labels) - 1) / 2)));
							break;
						case EngineVersion.S2:
							result.AddRange(ByteConverter.GetBytes((short)((LabelSubtract(lastlabel + "_End", lastlabel, labels) - 2) / 2)));
							break;
						case EngineVersion.S3K:
							result.AddRange(ByteConverter.GetBytes((short)((LabelSubtract(lastlabel + "_End", lastlabel, labels) - 4) / 2)));
							break;
					}
				}
				else if (ln[0].Equals("s3kPlayerDplcHeader"))
					result.AddRange(ByteConverter.GetBytes((short)((LabelSubtract(lastlabel + "_End", lastlabel, labels) - 2) / 2)));
				else if (ln[0].Equals("dplcEntry"))
				{
					string d = string.Empty;
					for (int i = 1; i < ln.Length; i++)
						d += ln[i];
					string[] dats = d.Split(',');
					unchecked { result.AddRange(new DPLCEntry((byte)ParseASMNum(dats[0], labels), (ushort)ParseASMNum(dats[1], labels)).GetBytes(version)); }
				}
				else if (ln[0].Equals("s3kPlayerDplcEntry"))
				{
					string d = string.Empty;
					for (int i = 1; i < ln.Length; i++)
						d += ln[i];
					string[] dats = d.Split(',');
					unchecked { result.AddRange(new DPLCEntry((byte)ParseASMNum(dats[0], labels), (ushort)ParseASMNum(dats[1], labels)).GetBytes(EngineVersion.S2)); }
				}*/
				else if (ln[0].Equals("obj1E67Size"))
					result.AddRange(ByteConverter.GetBytes((short)(LabelSubtract(lastlabel + "_End", lastlabel, labels) - 2)));
				else
					break;
			}
			return result.ToArray();
		}

		public static long ParseASMNum(string data, Dictionary<string, int> labels)
		{
			data = data.Trim();
			bool neg = false;
			if (data.StartsWith("-"))
			{
				neg = true;
				data = data.Substring(1);
			}
			long result;
			if (labels.ContainsKey(data))
				result = labels[data];
			else if (data.StartsWith("$"))
				result = long.Parse(data.Substring(1), System.Globalization.NumberStyles.HexNumber);
			else
				result = long.Parse(data, System.Globalization.NumberStyles.None, System.Globalization.NumberFormatInfo.InvariantInfo);
			if (neg)
				return -result;
			return result;
		}

		public static int ParseASMOffset(string data, Dictionary<string, int> labels)
		{
			return LabelSubtract(data.Split('-')[0], data.Split('-')[1], labels);
		}

		public static int LabelSubtract(string label1, string label2, Dictionary<string, int> labels)
		{
			return (labels.ContainsKey(label1) ? labels[label1] : 0) - (labels.ContainsKey(label2) ? labels[label2] : 0);
		}

		public static Dictionary<string, int> GetASMLabels(string file, EngineVersion version)
		{
			return GetASMLabels(file, 0, version);
		}

		public static Dictionary<string, int> GetASMLabels(string file, string label, EngineVersion version)
		{
			string[] fc = System.IO.File.ReadAllLines(file);
			int sti = -1;
			for (int i = 0; i < fc.Length; i++)
			{
				if (fc[i].StartsWith(label + ":"))
				{
					sti = i;
					fc[i] = fc[i].Substring(label.Length + 1);
				}
			}
			if (sti == -1) return new Dictionary<string, int>();
			return GetASMLabels(file, sti, version);
		}


		public static Dictionary<string, int> GetASMLabels(string file, int sti, EngineVersion version)
		{
			string[] fc = System.IO.File.ReadAllLines(file);
			Dictionary<string, int> labels = new Dictionary<string, int>();
			int curaddr = 0;
			for (int st = sti; st < fc.Length; st++)
			{
				string[] ln = fc[st].Split(';')[0].Trim().Split(new char[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
				if (ln.Length == 0) continue;
				if (!char.IsWhiteSpace(fc[st], 0))
				{
					string[] l = ln[0].Split(new char[] { ':' }, StringSplitOptions.RemoveEmptyEntries);
					labels.Add(l[0], curaddr);
					if (l.Length == 1)
					{
						string[] ln2 = new string[ln.Length - 1];
						for (int i = 0; i < ln2.Length; i++)
						{
							ln2[i] = ln[i + 1];
						}
						ln = ln2;
					}
					else
					{
						ln[0] = l[1];
					}
					if (ln.Length == 0) continue;
				}
				if (ln[0].Equals("even"))
				{
					if (curaddr % 2 == 1)
						curaddr++;
				}
				else if (ln[0].Equals("align"))
				{
					uint alignment = (uint)ParseASMNum(ln[1], labels);
					if (curaddr % alignment != 0)
						curaddr += (int)(alignment - (curaddr % alignment));
				}
				else if (ln[0].Equals("offsetTable") | ln[0].Equals("mappingsTable"))
					continue;
				else if (ln[0].StartsWith("offsetTableEntry.") | ln[0].StartsWith("mappingsTableEntry."))
					switch (ln[0].Split('.')[1])
					{
						case "b":
							curaddr++;
							break;
						case "w":
							curaddr += 2;
							break;
						case "l":
							curaddr += 4;
							break;
					}
				else if (ln[0].StartsWith("dc."))
				{
					string d = string.Empty;
					for (int i = 1; i < ln.Length; i++)
						d += ln[i];
					string[] dats = d.Split(',');
					switch (ln[0].Split('.')[1])
					{
						case "b":
							curaddr += dats.Length;
							break;
						case "w":
							curaddr += dats.Length * 2;
							break;
						case "l":
							curaddr += dats.Length * 4;
							break;
					}
				}
				else if (ln[0].Equals("spriteHeader") | ln[0].Equals("dplcHeader"))
					switch (version)
					{
						case EngineVersion.S1:
							curaddr++;
							break;
						case EngineVersion.S2:
						case EngineVersion.S3K:
							curaddr += 2;
							break;
					}
				else if (ln[0].Equals("s3kPlayerDplcHeader"))
					curaddr += 2;
				else if (ln[0].Equals("spritePiece") | ln[0].Equals("spritePiece2P"))
					switch (version)
					{
						case EngineVersion.S1:
							curaddr += 5;
							break;
						case EngineVersion.S2:
							curaddr += 8;
							break;
						case EngineVersion.S3K:
							curaddr += 6;
							break;
					}
				else if (ln[0].Equals("dplcEntry") | ln[0].Equals("s3kPlayerDplcEntry"))
					curaddr += 2;
				else
					break;
			}
			return labels;
		}

		/*public static byte[] ProcessDPLC(byte[] artfile, DPLCFrame dplc)
		{
			List<byte> result = new List<byte>();
			byte[] tmp;
			for (int i = 0; i < dplc.Count; i++)
			{
				tmp = new byte[dplc[i].TileCount * 0x20];
				Array.Copy(artfile, dplc[i].TileNum * 0x20, tmp, 0, tmp.Length);
				result.AddRange(tmp);
			}
			return result.ToArray();
		}*/

		public static Sprite MapFrameToBmp(byte[] art, MappingsFrame map, int startpal)
		{
			return MapFrameToBmp(art, map, null, startpal);
		}

		public static Sprite MapFrameToBmp(byte[] art, MappingsFrame map, DPLCFrame dplc, int startpal)
		{
			//if (dplc != null)
			//	art = ProcessDPLC(art, dplc);
			int left = 0;
			int right = 0;
			int top = 0;
			int bottom = 0;
			for (int i = 0; i < map.TileCount; i++)
			{
				left = Math.Min(map[i].X, left);
				right = Math.Max(map[i].X + (map[i].Width * 8), right);
				top = Math.Min(map[i].Y, top);
				bottom = Math.Max(map[i].Y + (map[i].Height * 8), bottom);
			}
			Point offset = new Point(left, top);
			//BitmapBits[] img = new BitmapBits[] { new BitmapBits(right - left, bottom - top), new BitmapBits(right - left, bottom - top) };
			BitmapBits img = new BitmapBits(right - left, bottom - top);
			for (int i = map.TileCount - 1; i >= 0; i--)
			{
				//int pr = map[i].Tile.Priority ? 1 : 0;
				//img[pr].DrawSprite(MapTileToBmp(art, map[i], startpal), -left, -top);
				img.DrawSprite(MapTileToBmp(art, map[i], startpal), -left, -top);
			}
			//return new Sprite[] { new Sprite(img[0], offset), new Sprite(img[1],offset) };
			return new Sprite(img, offset);
		}

		public static Sprite MapTileToBmp(byte[] art, MappingsTile map, int startpal)
		{
			BitmapBits pcbmp = new BitmapBits(map.Width * 8, map.Height * 8);
			int ti = 0;
			byte[] colorLUT = new byte[4];
			
			colorLUT[0] = 0;
			if (startpal >= 0)
			{
				for (int i = 0; i < 3; i++)
					colorLUT[1 + i] = BmpPalColLUT[startpal * 3 + i];
			}
			else
			{
				startpal = -startpal - 1;	// -1 -> 0, -2 -> 1
				for (int i = 0; i < 3; i++)
					colorLUT[1 + i] = (byte)(1 + startpal * 3 + i);
			}
			
			for (int x = 0; x < map.Width; x++)
			{
				for (int y = 0; y < map.Height; y++)
				{
					pcbmp.DrawBitmap(
						TileToBmp8bppLUT(art, map.Tile + ti, colorLUT),
						x * 8, y * 8);
					ti++;
				}
			}
			//pcbmp.Flip(map.Tile.XFlip, map.Tile.YFlip);
			return new Sprite(pcbmp, new Point(map.X, map.Y));
		}

		public static Color PaletteToColor(int line, int index, bool acceptTransparent)
		{
			if (index == 0)
			{
				if (acceptTransparent)
					return Color.Transparent;
				else
					return BGColor.RGBColor;
			}
			return Palette[CurPal][line, index].RGBColor;
		}

		public static void ColorToPalette(int line, int index, Color color)
		{
			SetPalColor(line, index, new SonLVLColor(color));
		}
		
		public static void SetPalColor(int line, int index, SonLVLColor color)
		{
			if (line >= 0x20)
			{
				Palette[CurPal][line, index] = color;
				return;
			}
			if (index <= 0)
				return;
			
			// changed BG/FG color - refresh all references
			ushort palCID;
			int pi = index - 1;
			palCID = (line >= 0x10) ? PalIDsFG.palIDs[line & 0x0F] : PalIDsBG.palIDs[line & 0x0F];
			PalCollection[palCID * 3 + pi] = color;
			
			for (int pa = 0; pa < 16; pa++)
			{
				ushort fgPalID = PalIDsFG.palIDs[pa];
				ushort bgPalID = PalIDsBG.palIDs[pa];
				if (PalIDsFG.palIDs[pa] == palCID)
					Palette[CurPal][0x10 + pa, index] = PalCollection[palCID * 3 + pi];
				if (PalIDsBG.palIDs[pa] == palCID)
					Palette[CurPal][0x00 + pa, index] = PalCollection[palCID * 3 + pi];
			}
		}

		public static void RedrawBlock(int block, bool drawChunks)
		{
			BlockBmpBits[block][0] = new BitmapBits(32, 32);
			BlockBmpBits[block][1] = new BitmapBits(32, 32);
			//CompBlockBmpBits[block] = new BitmapBits(32, 32);
			BlockColBmpBits[block] = new BitmapBits(32, 32);
			for (int by = 0; by < 4; by++)
			{
				for (int bx = 0; bx < 4; bx++)
				{
					PatternIndex pt = Blocks[block].Tiles[bx, by];
					// background and foreground have different palettes
					BitmapBits tile = TileToBmp8bpp(Tiles[pt.Tile], 0, pt.Palette);
					tile.Flip(pt.XFlip, pt.YFlip);
					BlockBmpBits[block][0].DrawBitmap(tile, bx * 8, by * 8);
					
					tile = TileToBmp8bpp(Tiles[pt.Tile], 0, 0x10 + pt.Palette);
					tile.Flip(pt.XFlip, pt.YFlip);
					BlockBmpBits[block][1].DrawBitmap(tile, bx * 8, by * 8);

					if (ColBmpBits != null && ColInds[pt.Tile] >= 0 && ColInds[pt.Tile] < ColBmpBits.Length)
						tile = new BitmapBits(ColBmpBits[ColInds[pt.Tile]]);
					else
						tile = new BitmapBits(8, 8);
					tile.Flip(pt.XFlip, pt.YFlip);
					BlockColBmpBits[block].DrawBitmap(tile, bx * 8, by * 8);
				}
			}
			//CompBlockBmpBits[block].DrawBitmap(BlockBmpBits[block][0], Point.Empty);
			//CompBlockBmpBits[block].DrawBitmapComposited(BlockBmpBits[block][1], Point.Empty);
			BlockBmps[block][0] = BlockBmpBits[block][0].ToBitmap(BmpPal);
			BlockBmps[block][1] = BlockBmpBits[block][1].ToBitmap(BmpPal);
			BlockBmpsA[0][block] = BlockBmps[block][0];
			BlockBmpsA[1][block] = BlockBmps[block][1];
			BlockColBmps[block] = BlockColBmpBits[block].ToBitmap(Color.Transparent, Color.White, Color.Yellow, Color.Black);
			//CompBlockBmps[block] = CompBlockBmpBits[block].ToBitmap(BmpPal);
			/*if (drawChunks)
			{
				for (int i = 0; i < Chunks.Count; i++)
				{
					bool dr = false;
					for (int k = 0; k < Level.ChunkHeight / 16; k++)
						for (int j = 0; j < Level.ChunkWidth / 16; j++)
							if (Chunks[i].Blocks[j, k].Block == block)
								dr = true;
					if (dr)
						RedrawChunk(i);
				}
			}*/
		}

		/*public static void RedrawChunk(int chunk)
		{
			ChunkBmpBits[chunk][0] = new BitmapBits(Level.ChunkWidth, Level.ChunkHeight);
			ChunkBmpBits[chunk][1] = new BitmapBits(Level.ChunkWidth, Level.ChunkHeight);
			ChunkColBmpBits[chunk][0] = new BitmapBits(Level.ChunkWidth, Level.ChunkHeight);
			ChunkColBmpBits[chunk][1] = new BitmapBits(Level.ChunkWidth, Level.ChunkHeight);
			CompChunkBmpBits[chunk] = new BitmapBits(Level.ChunkWidth, Level.ChunkHeight);
			for (int by = 0; by < Level.ChunkHeight / 16; by++)
			{
				for (int bx = 0; bx < Level.ChunkWidth / 16; bx++)
				{
					ChunkBlock blk = Chunks[chunk].Blocks[bx, by];
					if (blk.Block < BlockBmpBits.Count)
					{
						BitmapBits bmp = new BitmapBits(BlockBmpBits[blk.Block][0]);
						bmp.Flip(blk.XFlip, blk.YFlip);
						ChunkBmpBits[chunk][0].DrawBitmap(
							bmp,
							bx * 16, by * 16);
						bmp = new BitmapBits(BlockBmps[blk.Block][1]);
						bmp.Flip(blk.XFlip, blk.YFlip);
						ChunkBmpBits[chunk][1].DrawBitmap(
							bmp,
							bx * 16, by * 16);
						if (ColInds1.Count > 0)
						{
							bmp = new BitmapBits(ColBmpBits[GetColInd1(blk.Block)]);
							bmp.IncrementIndexes((int)blk.Solid1 - 1);
							bmp.Flip(blk.XFlip, blk.YFlip);
							ChunkColBmpBits[chunk][0].DrawBitmap(bmp, bx * 16, by * 16);
							if (blk is S2ChunkBlock)
							{
								bmp = new BitmapBits(ColBmpBits[GetColInd2(blk.Block)]);
								bmp.IncrementIndexes((int)((S2ChunkBlock)blk).Solid2 - 1);
								bmp.Flip(blk.XFlip, blk.YFlip);
								ChunkColBmpBits[chunk][1].DrawBitmap(bmp, bx * 16, by * 16);
							}
						}
					}
					else
					{
						BitmapBits bmp = new BitmapBits(InvalidBlock);
						bmp.Flip(blk.XFlip, blk.YFlip);
						ChunkBmpBits[chunk][0].DrawBitmap(
							bmp,
							bx * 16, by * 16);
					}
				}
			}
			CompChunkBmpBits[chunk].DrawBitmap(ChunkBmpBits[chunk][0], Point.Empty);
			CompChunkBmpBits[chunk].DrawBitmapComposited(ChunkBmpBits[chunk][1], Point.Empty);
			ChunkBmps[chunk][0] = ChunkBmpBits[chunk][0].ToBitmap(BmpPal);
			ChunkBmps[chunk][1] = ChunkBmpBits[chunk][1].ToBitmap(BmpPal);
			ChunkColBmps[chunk][0] = ChunkColBmpBits[chunk][0].ToBitmap(Color.Transparent, Color.White, Color.Yellow, Color.Black);
			ChunkColBmps[chunk][1] = ChunkColBmpBits[chunk][1].ToBitmap(Color.Transparent, Color.White, Color.Yellow, Color.Black);
			ChunkColBmpBits[chunk][0].IncrementIndexes(ColorWhite - 1);
			ChunkColBmpBits[chunk][1].IncrementIndexes(ColorWhite - 1);
			CompChunkBmps[chunk] = CompChunkBmpBits[chunk].ToBitmap(BmpPal);
		}*/

		public static ObjectDefinition GetObjectDefinition(byte ID)
		{
			if (ObjTypes != null && ObjTypes.ContainsKey(ID))
				return ObjTypes[ID];
			else
				return unkobj;
		}

		public static ObjectEntry CreateObject(byte ID)
		{
			ObjectDefinition def = GetObjectDefinition(ID);
			ObjectEntry oe = ObjectFormat.CreateObject();
			oe.ID = ID;
			oe.SubType = def.DefaultSubtype;
			//if (oe is RememberStateObjectEntry)
			//	((RememberStateObjectEntry)oe).RememberState = def.RememberState;
			return oe;
		}

		public static void AddObjPalette(int line)
		{
			for (int i=0; i < 3; i++)
				AddObjPaletteColor(line, i);
		}

		private static void AddObjPaletteColor(int line, int index)
		{
			const byte ibase = 1 + 32 * 3;
			int pccol = line * 3 + index;
			if (BmpPalColLUT[pccol] != 0)
				return;
			
			SonLVLColor palcol = PalCollection[pccol];
			for (byte i = ibase; i < BmpPalColMax; i++)
			{
				if (BmpPal.Entries[i].Equals(palcol))
				{
					// found color in BMP palette - set reference and return
					BmpPalColLUT[pccol] = i;
					return;
				}
			}
			if (BmpPalColMax < ColorFirstGlobal)
			{
				BmpPal.Entries[BmpPalColMax] = palcol.RGBColor;
				BmpPalColLUT[pccol] = BmpPalColMax;
				BmpPalColMax++;
			}
		}

		public static void PaletteChanged()
		{
			BmpPal.Entries[0] = PaletteToColor(0, 0, true);
			for (int i = 0; i < 32*3; i++)
				BmpPal.Entries[1+i] = PaletteToColor(i / 3, 1 + i % 3, true);
			BmpPal.Entries[ColorRed] = Color.Red;
			BmpPal.Entries[ColorWhite] = Color.White;
			BmpPal.Entries[ColorYellow] = Color.Yellow;
			BmpPal.Entries[ColorBlack] = Color.Black;
			foreach (Bitmap[] item in BlockBmps)
			{
				item[0].Palette = BmpPal;
				item[1].Palette = BmpPal;
			}
			/*foreach (Bitmap item in CompBlockBmps)
				item.Palette = BmpPal;
			foreach (Bitmap[] item in ChunkBmps)
			{
				item[0].Palette = BmpPal;
				item[1].Palette = BmpPal;
			}
			foreach (Bitmap item in CompChunkBmps)
				item.Palette = BmpPal;*/
			PaletteChangedEvent();
		}

		/*public static void RedrawCol(int block, bool drawChunks)
		{
			ColBmpBits[block] = new BitmapBits(32, 32);
			for (int by = 0; by < 32; by++)
			{
				for (int bx = 0; bx < 32; bx++)
				{
					switch (Math.Sign(ColArr1[block][bx]))
					{
						case -1:
							if (32 + ColArr1[block][bx] <= by)
								ColBmpBits[block].Bits[((32 - by) * 32) + bx] = 1;
							break;
						case 1:
							if (ColArr1[block][bx] > by)
								ColBmpBits[block].Bits[((32 - by) * 32) + bx] = 1;
							break;
					}
				}
			}
			ColBmps[block] = ColBmpBits[block].ToBitmap(Color.Transparent, Color.White);
			if (drawChunks)
			{
				for (int i = 0; i < Chunks.Count; i++)
				{
					bool dr = false;
					for (int k = 0; k < Level.ChunkHeight / 16; k++)
						for (int j = 0; j < Level.ChunkWidth / 16; j++)
							if (GetColInd1(Chunks[i].Blocks[j, k].Block) == block | GetColInd2(Chunks[i].Blocks[j, k].Block) == block)
								dr = true;
					if (dr)
						RedrawChunk(i);
				}
			}
		}*/

		/*public static sbyte[][] GenerateRotatedCollision()
		{
			sbyte[][] result = new sbyte[256][];
			for (int i = 0; i < 256; i++)
			{
				result[i] = new sbyte[16];
				for (int y = 0; y < 16; y++)
				{
					sbyte height = 0;
					int misses = 0;
					bool f = false;
					int st = 16;
					for (int x = 0; x < 16; x++)
					{
						switch (Math.Sign(ColArr1[i][15 - x]))
						{
							case 1:
								if (ColArr1[i][15 - x] >= 16 - y)
								{
									height = (sbyte)(x + 1);
									misses = 0;
									if (!f)
									{
										f = true;
										st = x;
									}
								}
								else
									misses++;
								break;
							case 0:
								misses++;
								break;
							case -1:
								if (ColArr1[i][15 - x] <= -y - 1)
								{
									height = (sbyte)(x + 1);
									misses = 0;
									if (!f)
									{
										f = true;
										st = x;
									}
								}
								else
									misses++;
								break;
						}
						if (x == 0 & misses == 1)
							break;
						if (misses == 3)
							break;
					}
					sbyte negheight = 0;
					misses = 0;
					f = false;
					int rst = 16;
					for (int x = 0; x < 16; x++)
					{
						switch (Math.Sign(ColArr1[i][x]))
						{
							case 1:
								if (ColArr1[i][x] >= 16 - y)
								{
									negheight = (sbyte)(-x - 1);
									misses = 0;
									if (!f)
									{
										f = true;
										rst = x;
									}
								}
								else
									misses++;
								break;
							case 0:
								misses++;
								break;
							case -1:
								if (ColArr1[i][x] <= -y - 1)
								{
									negheight = (sbyte)(-x - 1);
									misses = 0;
									if (!f)
									{
										f = true;
										rst = x;
									}
								}
								else
									misses++;
								break;
						}
						if (x == 0 & misses == 1)
							break;
						if (misses == 3)
							break;
					}
					result[i][y] = height;
					if (Math.Abs(negheight) > height || st > rst)
						result[i][y] = negheight;
					if (rst > st)
						result[i][y] = height;
				}
			}
			return result;
		}*/

		public static ImportResult BitmapToTiles(BitmapInfo bmpi, bool[,] priority, byte? forcepal, IList<byte[]> tiles, bool interlaced, bool optimize, Action updateProgress = null)
		{
			ImportResult result = new ImportResult(bmpi.Width / 8, bmpi.Height / 8);
			int pal = 0;
			bool match = false;
			byte[] tile, tileh, tilev, tilehv;
			PatternIndex map;
			//if (!interlaced)
				for (int y = 0; y < bmpi.Height / 8; y++)
					for (int x = 0; x < bmpi.Width / 8; x++)
					{
						map = new PatternIndex() { /*Priority = priority[x, y]*/ };
						tile = BmpToTile(new BitmapInfo(bmpi, x * 8, y * 8, 8, 8), forcepal, out pal);
						tileh = FlipTile(tile, true, false);
						tilev = FlipTile(tile, false, true);
						tilehv = FlipTile(tileh, false, true);
						map.Palette = (byte)pal;
						match = false;
						if (optimize)
							for (int i = 0; i < tiles.Count; i++)
							{
								if (tiles[i].FastArrayEqual(tile))
								{
									match = true;
									map.Tile = (ushort)i;
									break;
								}
								if (tiles[i].FastArrayEqual(tileh))
								{
									match = true;
									map.Tile = (ushort)i;
									map.XFlip = true;
									break;
								}
								if (tiles[i].FastArrayEqual(tilev))
								{
									match = true;
									map.Tile = (ushort)i;
									map.YFlip = true;
									break;
								}
								if (tiles[i].FastArrayEqual(tilehv))
								{
									match = true;
									map.Tile = (ushort)i;
									map.XFlip = true;
									map.YFlip = true;
									break;
								}
							}
						if (!match)
						{
							tiles.Add(tile);
							result.Art.Add(tile);
							map.Tile = (ushort)(tiles.Count - 1);
						}
						result.Mappings[x, y] = map;
					}
			/*else
				for (int y = 0; y < bmpi.Height / 16; y++)
					for (int x = 0; x < bmpi.Width / 8; x++)
					{
						map = new PatternIndex() { Priority = priority[x, y] };
						tile = BmpToTileInterlaced(new BitmapInfo(bmpi, x * 8, y * 16, 8, 16), forcepal, out pal);
						tileh = FlipTileInterlaced(tile, true, false);
						tilev = FlipTileInterlaced(tile, false, true);
						tilehv = FlipTileInterlaced(tileh, false, true);
						map.Palette = (byte)pal;
						match = false;
						if (optimize)
							for (int i = 0; i < tiles.Count; i++)
							{
								if (tiles[i].FastArrayEqual(tile))
								{
									match = true;
									map.Tile = (ushort)i;
									break;
								}
								if (tiles[i].FastArrayEqual(tileh))
								{
									match = true;
									map.Tile = (ushort)i;
									map.XFlip = true;
									break;
								}
								if (tiles[i].FastArrayEqual(tilev))
								{
									match = true;
									map.Tile = (ushort)i;
									map.YFlip = true;
									break;
								}
								if (tiles[i].FastArrayEqual(tilehv))
								{
									match = true;
									map.Tile = (ushort)i;
									map.XFlip = true;
									map.YFlip = true;
									break;
								}
							}
						if (!match)
						{
							tiles.Add(tile);
							byte[] t1 = new byte[32];
							Array.Copy(tile, 0, t1, 0, 32);
							result.Art.Add(t1);
							byte[] t2 = new byte[32];
							Array.Copy(tile, 32, t2, 0, 32);
							result.Art.Add(t2);
							map.Tile = (ushort)((tiles.Count - 1) * 2);
						}
						result.Mappings[x, y * 2] = map;
						result.Mappings[x, y * 2 + 1] = map.Clone();
						result.Mappings[x, y * 2 + 1].Tile ^= 1;
					}*/
			return result;
		}

		public static byte[] BmpToTile(BitmapInfo bmp, byte? forcepal, out int palette)
		{
			BitmapBits bmpbits = new BitmapBits(8, 8);
			switch (bmp.PixelFormat)
			{
				case PixelFormat.Format1bppIndexed:
					LoadBitmap1BppIndexed(bmpbits, bmp.Pixels, bmp.Stride);
					palette = forcepal ?? 0;
					return bmpbits.ToTile();
				case PixelFormat.Format32bppArgb:
					Color[,] pixels = new Color[8, 8];
					for (int y = 0; y < bmp.Height; y++)
					{
						int srcaddr = y * Math.Abs(bmp.Stride);
						for (int x = 0; x < bmp.Width; x++)
							pixels[x, y] = Color.FromArgb(BitConverter.ToInt32(bmp.Pixels, srcaddr + (x * 4)));
					}
					palette = forcepal ?? 0;
					Color[] newpal = new Color[4];
					newpal[0] = PaletteToColor(0, 0, false);
					if (!forcepal.HasValue)
					{
						long mindist = long.MaxValue;
						for (int i = 0; i < 32; i++)
						{
							Array.Copy(BmpPal.Entries, 1 + i * 3, newpal, 1, 3);
							long totdist = 0;
							int dist;
							for (int y = 0; y < 8; y++)
								for (int x = 0; x < 8; x++)
									if (pixels[x, y].A >= 128)
									{
										pixels[x, y].FindNearestMatch(out dist, newpal);
										totdist += dist;
									}
							if (totdist < mindist)
							{
								palette = i;
								mindist = totdist;
							}
						}
					}
					Array.Copy(BmpPal.Entries, 1 + palette * 3, newpal, 1, 3);
					for (int y = 0; y < 8; y++)
						for (int x = 0; x < 8; x++)
							if (pixels[x, y].A >= 128)
								bmpbits[x, y] = (byte)Array.IndexOf(newpal, pixels[x, y].FindNearestMatch(newpal));
					break;
				case PixelFormat.Format4bppIndexed:
					LoadBitmap4BppIndexed(bmpbits, bmp.Pixels, bmp.Stride);
					palette = forcepal ?? 0;
					break;
				case PixelFormat.Format8bppIndexed:
					LoadBitmap8BppIndexed(bmpbits, bmp.Pixels, bmp.Stride);
					int[] palcnt = new int[32];
					for (int y = 0; y < 8; y++)
						for (int x = 0; x < 8; x++)
						{
							if (bmpbits[x, y] > 0)
							{
								int palidx = (bmpbits[x, y] - 1) / 3;
								palcnt[palidx & 0x1F]++;
								bmpbits[x, y] -= (byte)(palidx * 3);
							}
						}
					palette = forcepal ?? 0;
					if (!forcepal.HasValue)
					{
						for (int pi = 1; pi < 32; pi ++)
						{
							if (palcnt[pi] > palcnt[palette])
								palette = pi;
						}
					}
					break;
				default:
					throw new Exception("wat");
			}
			return bmpbits.ToTile();
		}

		/*public static byte[] BmpToTileInterlaced(BitmapInfo bmp, byte? forcepal, out int palette)
		{
			BitmapBits bmpbits = new BitmapBits(8, 16);
			switch (bmp.PixelFormat)
			{
				case PixelFormat.Format1bppIndexed:
					LoadBitmap1BppIndexed(bmpbits, bmp.Pixels, bmp.Stride);
					palette = forcepal ?? 0;
					return bmpbits.ToTile();
				case PixelFormat.Format32bppArgb:
					Color[,] pixels = new Color[8, 16];
					for (int y = 0; y < bmp.Height; y++)
					{
						int srcaddr = y * Math.Abs(bmp.Stride);
						for (int x = 0; x < bmp.Width; x++)
							pixels[x, y] = Color.FromArgb(BitConverter.ToInt32(bmp.Pixels, srcaddr + (x * 4)));
					}
					palette = forcepal ?? 0;
					Color[] newpal = new Color[16];
					if (!forcepal.HasValue)
					{
						int mindist = int.MaxValue;
						for (int i = 0; i < 4; i++)
						{
							Array.Copy(BmpPal.Entries, i * 16, newpal, 0, 16);
							int totdist = 0;
							int dist;
							for (int y = 0; y < 16; y++)
								for (int x = 0; x < 8; x++)
									if (pixels[x, y].A >= 128)
									{
										pixels[x, y].FindNearestMatch(out dist, newpal);
										totdist += dist;
									}
							if (totdist < mindist)
							{
								palette = i;
								mindist = totdist;
							}
						}
					}
					Array.Copy(BmpPal.Entries, palette * 16, newpal, 0, 16);
					for (int y = 0; y < 16; y++)
						for (int x = 0; x < 8; x++)
							if (pixels[x, y].A >= 128)
								bmpbits[x, y] = (byte)Array.IndexOf(newpal, pixels[x, y].FindNearestMatch(newpal));
					break;
				case PixelFormat.Format4bppIndexed:
					LoadBitmap4BppIndexed(bmpbits, bmp.Pixels, bmp.Stride);
					palette = forcepal ?? 0;
					break;
				case PixelFormat.Format8bppIndexed:
					LoadBitmap8BppIndexed(bmpbits, bmp.Pixels, bmp.Stride);
					int[] palcnt = new int[4];
					for (int y = 0; y < 16; y++)
						for (int x = 0; x < 8; x++)
						{
							if ((bmpbits[x, y] & 15) > 0)
								palcnt[bmpbits[x, y] / 16]++;
							bmpbits[x, y] &= 15;
						}
					palette = forcepal ?? 0;
					if (!forcepal.HasValue)
					{
						if (palcnt[1] > palcnt[palette])
							palette = 1;
						if (palcnt[2] > palcnt[palette])
							palette = 2;
						if (palcnt[3] > palcnt[palette])
							palette = 3;
					}
					break;
				default:
					throw new Exception("wat");
			}
			return bmpbits.ToTileInterlaced();
		}*/

		/*public static ColInfo[,] GetColMap(Bitmap bmp)
		{
			if (bmp.PixelFormat != PixelFormat.Format32bppArgb)
				bmp = bmp.To32bpp();
			BitmapBits bmpbits = new BitmapBits(bmp.Width, bmp.Height);
			BitmapData bmpd = bmp.LockBits(new Rectangle(0, 0, bmp.Width, bmp.Height), ImageLockMode.ReadOnly, bmp.PixelFormat);
			int stride = bmpd.Stride;
			byte[] Bits = new byte[Math.Abs(stride) * bmpd.Height];
			System.Runtime.InteropServices.Marshal.Copy(bmpd.Scan0, Bits, 0, Bits.Length);
			bmp.UnlockBits(bmpd);
			LoadBitmap32BppArgb(bmpbits, Bits, stride, new Color[] { Color.Magenta, Color.White, Color.Yellow, Color.Black });
			ColInfo[,] result = new ColInfo[bmpbits.Width / 16, bmpbits.Height / 16];
			for (int by = 0; by < bmpbits.Height / 16; by++)
				for (int bx = 0; bx < bmpbits.Width / 16; bx++)
				{
					ushort[] coltypes = new ushort[3];
					sbyte[] heightmap = new sbyte[16];
					Point? start = null;
					Point? end = null;
					bool inverted = false;
					for (int x = 0; x < 16; x++)
					{
						if (bmpbits[bx * 16 + x, by * 16 + 15] != 0) // solidity starts at bottom
						{
							for (int y = 15; y >= 0; y--)
								if (bmpbits[bx * 16 + x, by * 16 + y] != 0)
									coltypes[bmpbits[bx * 16 + x, by * 16 + y] - 1]++;
								else
								{
									if (!start.HasValue)
										start = new Point(x, y + 1);
									end = new Point(x, y + 1);
									heightmap[x] = (sbyte)(15 - y);
									break;
								}
							if (heightmap[x] == 0)
							{
								if (!start.HasValue || start.Value.Y == 0)
									start = new Point(x, 0);
								if (!end.HasValue || end.Value.Y != 0)
									end = new Point(x, 0);
								heightmap[x] = 16;
							}
						}
						else if (bmpbits[bx * 16 + x, by * 16] != 0) // solidity starts at top
						{
							inverted = true;
							for (int y = 0; y < 16; y++)
								if (bmpbits[bx * 16 + x, by * 16 + y] != 0)
									coltypes[bmpbits[bx * 16 + x, by * 16 + y] - 1]++;
								else
								{
									if (!start.HasValue)
										start = new Point(x, y - 1);
									end = new Point(x, y - 1);
									heightmap[x] = (sbyte)-y;
									break;
								}
						}
					}
					if (inverted)
					{
						if (start.HasValue && start.Value.Y == 0)
							start = new Point(start.Value.X, 15);
						if (end.HasValue && end.Value.Y == 0)
							end = new Point(end.Value.X, 15);
						for (int x = 0; x < 16; x++)
							if (heightmap[x] == 16)
								heightmap[x] = -16;
					}
					Solidity solid = Solidity.NotSolid;
					byte angle = 0;
					if (start.HasValue)
					{
						solid = Solidity.TopSolid;
						ushort max = coltypes[0];
						if (coltypes[1] > max)
						{
							solid = Solidity.LRBSolid;
							max = coltypes[1];
						}
						if (coltypes[2] > max)
							solid = Solidity.AllSolid;
						if (start.Value.Y == end.Value.Y)
							angle = 0xFF;
						else
							angle = (byte)((byte)(Math.Atan2(end.Value.Y - start.Value.Y, (end.Value.X - start.Value.X) * (inverted ? -1 : 1)) * (256 / (2 * Math.PI))) & 0xFC);
					}
					result[bx, by] = new ColInfo(solid, heightmap, angle);
				}
			return result;
		}*/

		/*public static void GetPriMap(Bitmap bmp, bool[,] primap)
		{
			if (bmp.PixelFormat != PixelFormat.Format32bppArgb)
				bmp = bmp.To32bpp();
			BitmapBits bmpbits = new BitmapBits(bmp.Width, bmp.Height);
			BitmapData bmpd = bmp.LockBits(new Rectangle(0, 0, bmp.Width, bmp.Height), ImageLockMode.ReadOnly, bmp.PixelFormat);
			int stride = bmpd.Stride;
			byte[] Bits = new byte[Math.Abs(stride) * bmpd.Height];
			System.Runtime.InteropServices.Marshal.Copy(bmpd.Scan0, Bits, 0, Bits.Length);
			bmp.UnlockBits(bmpd);
			LoadBitmap32BppArgb(bmpbits, Bits, stride, new Color[] { Color.Black, Color.White });
			int w = Math.Min(primap.GetLength(0), bmpbits.Width / 8);
			int h = Math.Min(primap.GetLength(1), bmpbits.Height / 8);
			for (int ty = 0; ty < h; ty++)
				for (int tx = 0; tx < w; tx++)
				{
					ushort[] cnt = new ushort[2];
					for (int y = 0; y < 8; y++)
						for (int x = 0; x < 8; x++)
							cnt[bmpbits[(tx * 8) + x, (ty * 8) + y]]++;
					primap[tx, ty] = cnt[0] < cnt[1];
				}
		}*/

		private static void LoadBitmap1BppIndexed(BitmapBits bmp, byte[] Bits, int Stride)
		{
			for (int y = 0; y < bmp.Height; y++)
			{
				int srcaddr = y * Math.Abs(Stride);
				for (int x = 0; x < bmp.Width; x += 8)
				{
					bmp[x + 0, y] = (byte)((Bits[srcaddr + (x / 8)] >> 7) & 1);
					bmp[x + 1, y] = (byte)((Bits[srcaddr + (x / 8)] >> 6) & 1);
					bmp[x + 2, y] = (byte)((Bits[srcaddr + (x / 8)] >> 5) & 1);
					bmp[x + 3, y] = (byte)((Bits[srcaddr + (x / 8)] >> 4) & 1);
					bmp[x + 4, y] = (byte)((Bits[srcaddr + (x / 8)] >> 3) & 1);
					bmp[x + 5, y] = (byte)((Bits[srcaddr + (x / 8)] >> 2) & 1);
					bmp[x + 6, y] = (byte)((Bits[srcaddr + (x / 8)] >> 1) & 1);
					bmp[x + 7, y] = (byte)(Bits[srcaddr + (x / 8)] & 1);
				}
			}
		}

		private static void LoadBitmap32BppArgb(BitmapBits bmp, byte[] Bits, int Stride, Color[] palette)
		{
			for (int y = 0; y < bmp.Height; y++)
			{
				int srcaddr = y * Math.Abs(Stride);
				for (int x = 0; x < bmp.Width; x++)
				{
					Color col = Color.FromArgb(BitConverter.ToInt32(Bits, srcaddr + (x * 4)));
					bmp[x, y] = (byte)Array.IndexOf(palette, col.FindNearestMatch(palette));
					if (col.A < 128)
						bmp[x, y] = 0;
				}
			}
		}

		public static void LoadBitmap4BppIndexed(BitmapBits bmp, byte[] Bits, int Stride)
		{
			for (int y = 0; y < bmp.Height; y++)
			{
				int srcaddr = y * Math.Abs(Stride);
				for (int x = 0; x < bmp.Width; x += 2)
				{
					bmp[x, y] = (byte)(Bits[srcaddr + (x / 2)] >> 4);
					bmp[x + 1, y] = (byte)(Bits[srcaddr + (x / 2)] & 0xF);
				}
			}
		}

		private static void LoadBitmap8BppIndexed(BitmapBits bmp, byte[] Bits, int Stride)
		{
			for (int y = 0; y < bmp.Height; y++)
			{
				int srcaddr = y * Math.Abs(Stride);
				for (int x = 0; x < bmp.Width; x++)
					bmp[x, y] = Bits[srcaddr + x];
			}
		}

		public static void UpdateTileArray()
		{
			List<byte> tils = new List<byte>();
			foreach (byte[] item in Tiles)
				tils.AddRange(item);
			TileArray = tils.ToArray();
		}

		public static void Log(params string[] message) { LogEvent(message); }

		public static Bitmap BitmapBitsToBitmap(BitmapBits bmp)
		{
			return bmp.ToBitmap(BmpPal);
		}

		public static void Pad<T>(ref T[] array, int multiple)
		{
			int off = array.Length % multiple;
			if (off == 0) return;
			Array.Resize(ref array, array.Length + (multiple - off));
		}

		public static Size FGSize { get { return new Size(FGWidth, FGHeight); } }

		public static int FGWidth { get { return Layout.FGLayout.GetLength(0); } }
		public static int FGHeight { get { return Layout.FGLayout.GetLength(1); } }

		public static Size BGSize { get { return new Size(BGWidth, BGHeight); } }

		public static int BGWidth { get { return Layout.BGLayout.GetLength(0); } }
		public static int BGHeight { get { return Layout.BGLayout.GetLength(1); } }

		public static void ResizeFG(Size newSize) { ResizeFG(newSize.Width, newSize.Height); }

		public static void ResizeFG(int width, int height)
		{
			ushort[,] newFG = new ushort[width, height];
			//bool[,] newFGLoop = Layout.FGLoop != null ? new bool[width, height] : null;
			Size oldsize = FGSize;
			for (int y = 0; y < Math.Min(height, oldsize.Height); y++)
				for (int x = 0; x < Math.Min(width, oldsize.Width); x++)
				{
					newFG[x, y] = Layout.FGLayout[x, y];
					//if (newFGLoop != null)
					//	newFGLoop[x, y] = Layout.FGLoop[x, y];
				}
			Layout.FGLayout = newFG;
			//Layout.FGLoop = newFGLoop;
		}

		public static void ResizeBG(Size newSize) { ResizeBG(newSize.Width, newSize.Height); }

		public static void ResizeBG(int width, int height)
		{
			ushort[,] newBG = new ushort[width, height];
			//bool[,] newBGLoop = Layout.BGLoop != null ? new bool[width, height] : null;
			Size oldsize = BGSize;
			for (int y = 0; y < Math.Min(height, oldsize.Height); y++)
				for (int x = 0; x < Math.Min(width, oldsize.Width); x++)
				{
					newBG[x, y] = Layout.BGLayout[x, y];
					//if (newBGLoop != null)
					//	newBGLoop[x, y] = Layout.BGLoop[x, y];
				}
			Layout.BGLayout = newBG;
			//Layout.BGLoop = newBGLoop;
		}

		public static int GetBlockMax()
		{
			int blockmax = 0x400;
			switch (Game.EngineVersion)
			{
				/*case EngineVersion.S2:
				case EngineVersion.S2NA:
				case EngineVersion.S3K:
				case EngineVersion.SKC:
					blockmax = 0x300;
					break;*/
				case EngineVersion.SPA:
					// Cosmic Casino uses 0x14D8 blocks
					// The actual limit is 0x10000, though that would be 2 MB of data.
					blockmax = 0x2000;
					break;
			}
			if (Game.BlockMax.HasValue)
				blockmax = Game.BlockMax.Value;
			return blockmax;
		}

		public static byte GetColInd(int index)
		{
			if (index >= ColInds.Count)
				return 0;
			if (ColInds[index] < 0)
				return 0;
			return (byte)ColInds[index];
		}

		/*public static byte GetColInd1(int index)
		{
			if (index >= ColInds1.Count)
				return 0;
			return ColInds1[index];
		}

		public static byte GetColInd2(int index)
		{
			if (index >= ColInds2.Count)
				return 0;
			return ColInds2[index];
		}*/

		public static byte[] FlipTile(byte[] tile, bool xflip, bool yflip)
		{
			int mode = (xflip ? 1 : 0) | (yflip ? 2 : 0);
			switch (mode)
			{
				default:
					return tile;
				case 1:
					List<byte> tileh = new List<byte>();
					for (int ty = 0; ty < 8; ty++)
					{
						ushort px = ByteConverter.ToUInt16(tile, ty * 2);
						ushort newpx = 0x0000;
						for (int tx = 0; tx < 8; tx++)
						{
							newpx <<= 2;
							newpx |= (ushort)(px & 0x03);
							px >>= 2;
						}
						tileh.AddRange(ByteConverter.GetBytes(newpx));
					}
					return tileh.ToArray();
				case 2:
					byte[] tilev = new byte[16];
					for (int ty = 0; ty < 8; ty++)
						Array.Copy(tile, ty * 2, tilev, (7 - ty) * 2, 2);
					return tilev;
				case 3:
					List<byte> tilehv = new List<byte>();
					for (int ty = 0; ty < 8; ty++)
					{
						ushort px = ByteConverter.ToUInt16(tile, (7 - ty) * 2);
						ushort newpx = 0x0000;
						for (int tx = 0; tx < 8; tx++)
						{
							newpx <<= 2;
							newpx |= (ushort)(px & 0x03);
							px >>= 2;
						}
						tilehv.AddRange(ByteConverter.GetBytes(newpx));
					}
					return tilehv.ToArray();
			}
		}

		/*public static byte[] FlipTileInterlaced(byte[] tile, bool xflip, bool yflip)
		{
			int mode = (xflip ? 1 : 0) | (yflip ? 2 : 0);
			switch (mode)
			{
				default:
					return tile;
				case 1:
					byte[] tileh = new byte[64];
					for (int ty = 0; ty < 16; ty++)
						for (int tx = 0; tx < 4; tx++)
						{
							byte px = tile[(ty * 4) + tx];
							tileh[(ty * 4) + (3 - tx)] = (byte)((px >> 4) | (px << 4));
						}
					return tileh;
				case 2:
					byte[] tilev = new byte[64];
					for (int ty = 0; ty < 16; ty++)
						Array.Copy(tile, ty * 4, tilev, (15 - ty) * 4, 4);
					return tilev;
				case 3:
					byte[] tilehv = new byte[64];
					for (int ty = 0; ty < 16; ty++)
						for (int tx = 0; tx < 4; tx++)
						{
							byte px = tile[(ty * 4) + tx];
							tilehv[((15 - ty) * 4) + (3 - tx)] = (byte)((px >> 4) | (px << 4));
						}
					return tilehv;
			}
		}*/

		public static void RemapLayouts(Action<ushort[,], int, int> func)
		{
			for (int y = 0; y < FGHeight; y++)
				for (int x = 0; x < FGWidth; x++)
					func(Layout.FGLayout, x, y);
			for (int y = 0; y < BGHeight; y++)
				for (int x = 0; x < BGWidth; x++)
					func(Layout.BGLayout, x, y);
			foreach (var item in AdditionalLayouts.Values)
			{
				if (item.Value.FGLayout != null)
					for (int y = 0; y < item.Value.FGLayout.GetLength(1); y++)
						for (int x = 0; x < item.Value.FGLayout.GetLength(0); x++)
							func(item.Value.FGLayout, x, y);
				if (item.Value.BGLayout != null)
					for (int y = 0; y < item.Value.BGLayout.GetLength(1); y++)
						for (int x = 0; x < item.Value.BGLayout.GetLength(0); x++)
							func(item.Value.BGLayout, x, y);
			}
		}
	}

	public class LayoutData
	{
		public ushort[,] FGLayout;
		//public bool[,] FGLoop;
		public ushort[,] BGLayout;
		//public bool[,] BGLoop;
	}

	public enum EngineVersion
	{
		Invalid,
		SPA,
		S1,
		S2NA,
		S2,
		S3K,
		SCD,
		SCDPC,
		SKC,
		Chaotix,
		Custom
	}

	public enum TimeZone
	{
		None,
		Present,
		Past,
		Future
	}

	public enum MappingsFormat
	{
		Invalid,
		Binary,
		ASM,
		Macro
	}

	/*[Serializable]
	public class ColInfo
	{
		public Solidity Solidity { get; private set; }
		public sbyte[] HeightMap { get; private set; }
		public byte Angle { get; private set; }

		public ColInfo(Solidity solidity, sbyte[] heightMap, byte angle)
		{
			Solidity = solidity;
			HeightMap = heightMap;
			Angle = angle;
		}
	}*/

	public class BitmapInfo
	{
		public int Width { get; private set; }
		public int Height { get; private set; }
		public Size Size { get { return new Size(Width, Height); } }
		public PixelFormat PixelFormat { get; private set; }
		public int Stride { get; private set; }
		public byte[] Pixels { get; private set; }

		public BitmapInfo(Bitmap bitmap)
		{
			Width = bitmap.Width;
			Height = bitmap.Height;
			switch (bitmap.PixelFormat)
			{
				case PixelFormat.Format1bppIndexed:
				case PixelFormat.Format32bppArgb:
				case PixelFormat.Format4bppIndexed:
				case PixelFormat.Format8bppIndexed:
					PixelFormat = bitmap.PixelFormat;
					break;
				default:
					bitmap = bitmap.To32bpp();
					PixelFormat = System.Drawing.Imaging.PixelFormat.Format32bppArgb;
					break;
			}
			BitmapData bmpd = bitmap.LockBits(new Rectangle(0, 0, Width, Height), ImageLockMode.ReadOnly, PixelFormat);
			Stride = Math.Abs(bmpd.Stride);
			Pixels = new byte[Stride * Height];
			System.Runtime.InteropServices.Marshal.Copy(bmpd.Scan0, Pixels, 0, Pixels.Length);
			bitmap.UnlockBits(bmpd);
		}

		public BitmapInfo(BitmapInfo source, int x, int y, int width, int height)
		{
			switch (source.PixelFormat)
			{
				case System.Drawing.Imaging.PixelFormat.Format1bppIndexed:
					if (x % 8 != 0)
						throw new FormatException("X coordinate of 1bpp image section must be multiple of 8.");
					if (width % 8 != 0)
						throw new FormatException("Width of 1bpp image section must be multiple of 8.");
					break;
				case System.Drawing.Imaging.PixelFormat.Format4bppIndexed:
					if (x % 2 != 0)
						throw new FormatException("X coordinate of 4bpp image section must be multiple of 2.");
					if (width % 2 != 0)
						throw new FormatException("Width of 4bpp image section must be multiple of 2.");
					break;
			}
			Width = width;
			Height = height;
			PixelFormat = source.PixelFormat;
			switch (PixelFormat)
			{
				case PixelFormat.Format1bppIndexed:
					Stride = width / 8;
					x /= 8;
					break;
				case PixelFormat.Format4bppIndexed:
					Stride = width / 2;
					x /= 2;
					break;
				case PixelFormat.Format8bppIndexed:
					Stride = width;
					break;
				case PixelFormat.Format32bppArgb:
					Stride = width * 4;
					x *= 4;
					break;
			}
			Pixels = new byte[height * Stride];
			for (int v = 0; v < height; v++)
				Array.Copy(source.Pixels, ((y + v) * source.Stride) + x, Pixels, v * Stride, Stride);
		}

		public Bitmap ToBitmap()
		{
			Bitmap bitmap = new Bitmap(Width, Height, PixelFormat);
			BitmapData bmpd = bitmap.LockBits(new Rectangle(0, 0, Width, Height), ImageLockMode.WriteOnly, PixelFormat);
			byte[] bmpbits = new byte[Height * Math.Abs(bmpd.Stride)];
			for (int y = 0; y < Height; y++)
				Array.Copy(Pixels, y * Stride, bmpbits, y * Math.Abs(bmpd.Stride), Width);
			System.Runtime.InteropServices.Marshal.Copy(bmpbits, 0, bmpd.Scan0, bmpbits.Length);
			bitmap.UnlockBits(bmpd);
			return bitmap;
		}
	}

	public class ImportResult
	{
		public PatternIndex[,] Mappings { get; private set; }
		public List<byte[]> Art { get; private set; }

		public ImportResult(int width, int height)
		{
			Mappings = new PatternIndex[width, height];
			Art = new List<byte[]>();
		}
	}
}
