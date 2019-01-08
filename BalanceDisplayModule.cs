using Microsoft.SPOT;
using Microsoft.SPOT.Hardware;
using System;
using System.Collections;
using System.Threading;
using CTRE.HERO;

namespace CTRE
{
    namespace Gadgeteer
    {
        namespace Module
        {
            public class BalanceDisplayModule : ModuleBase
            {
                public enum Color : uint
                {
                    Black = 0x00000000,
                    Blue = 0x00ff0000,
                    Green = 0x0000ff00,
                    Red = 0x000000ff,
                    Cyan = 0x00ffff00,
                    Yellow = 0x0000ffff,
                    Magenta = 0x00ff00ff,
                    Orange = 0x00008fff,
                    White = 0x00ffffff,
                    Gray = 0x00a0a0a0,
                }

                public enum OrientationType
                {
                    Portrait = 0,
                    Landscape = 90,
                    Portrait_UpsideDown = 180,
                    Landscape_UpsideDown = 270,
                }

                public abstract class Sprite
                {
                    protected BalanceDisplayModule _container;
                    protected bool _dirty = true;
                    protected bool _passDirty = true;
                    protected bool _moved = false;
                    protected int[] _pos = { 0, 0 }; // x,y
                    protected int[] _size = { 0, 0 }; // w,h
                    protected bool _bEraseOldAfterMove = true;
                    /// <summary>
                    /// Register the new position to apply.  This way we could erase the spirte at the old position if need be.
                    /// </summary>
                    protected int[] _newPos = { 0, 0 };

                    private static Hashtable _mapBmp = new Hashtable();

                    /// <summary>
                    /// Only create new bitmaps for new width/height pairs.  This saves memory as text labels tend to be the same dimensions when tabled.
                    /// </summary>
                    /// <returns></returns>
                    protected Bitmap AllocBmp()
                    {
                        return AllocBmp(_size[0], _size[1]);
                    }
                    protected Bitmap AllocBmp(int w, int h)
                    {
                        ulong key = (ushort)w;
                        key <<= 16;
                        key |= (ushort)h;

                        if (_mapBmp.Contains(key))
                            return (Bitmap)_mapBmp[key];

                        Bitmap retval = new Bitmap(w, h);
                        _mapBmp[key] = retval;
                        return retval;
                    }
                    public Sprite(BalanceDisplayModule container, int x, int y, bool EraseOldAfterMove = true)
                    {
                        _container = container;
                        _pos[0] = x;
                        _pos[1] = y;
                        _newPos[0] = x;
                        _newPos[1] = y;
                        _size[0] = 0;
                        _size[1] = 0;
                        
                    }
                    public Sprite(BalanceDisplayModule container, int x, int y, int w, int h, bool EraseOldAfterMove = true)
                    {
                        _container = container;
                        _pos[0] = x;
                        _pos[1] = y;
                        _newPos[0] = x;
                        _newPos[1] = y;
                        _size[0] = w;
                        _size[1] = h;
                        _bEraseOldAfterMove = EraseOldAfterMove;
                    }
                    public Sprite ForceRefresh()
                    {
                        Dirty = true;
                        _container._dirtyEvent.Set();
                        return this;
                    }
                    public Sprite SetPosition(int x, int y)
                    {
                        if ((_newPos[0] == x) && (_newPos[1] == y))
                        {
                            /* no change */
                        }
                        else if (_bEraseOldAfterMove)
                        {
                            /* save the new loc, later we will erase the old, then paint the new */
                            _newPos[0] = x;
                            _newPos[1] = y;
                            ReadyToMove = true;
                            ForceRefresh();

                        } else { 
                            /* just repaint at the new loc */
                            _pos[0] = x;
                            _pos[1] = y;
                            _newPos[0] = x;
                            _newPos[1] = y;
                            ForceRefresh();
                        }
                        return this;
                    }
                    public Sprite SetSize(int width, int height)
                    {
                        SetWidth(width);
                        SetHeight(height);
                        return this;
                    }
                    public int PositionX
                    {
                        get { return _newPos[0]; }
                    }
                    public int PositionY
                    {
                        get { return _newPos[1]; }
                    }
                    public int Width
                    {
                        get { return _size[0]; }
                    }
                    public int Height
                    {
                        get { return _size[1]; }
                    }
                    public Sprite SetWidth(int width)
                    {
                        if( _size[0] != width)
                        {
                            _size[0] = width;
                            ForceRefresh();
                        }
                        return this;
                    }
                    public Sprite SetHeight(int height)
                    {
                        if (_size[1] != height)
                        {
                            _size[1] = height;
                            ForceRefresh();
                        }
                        return this;
                    }
                    internal bool Dirty
                    {
                        get
                        {
                            lock (this)
                            {
                                /* don't pass it up if disabled */
                                if (_passDirty == false)
                                    return false;
                                bool retval = _dirty;
                                _dirty = false;
                                return retval;
                            }
                        }
                        set
                        {
                            lock (this)
                            {
                                _dirty = value;
                            }
                        }
                    }
                    internal bool ReadyToMove
                    {
                        get
                        {
                            lock (this)
                            {
                                bool retval = _moved;
                                _moved = false;
                                return retval;
                            }
                        }
                        set
                        {
                            lock (this)
                            {
                                _moved = value;
                            }
                        }
                    }


                    public void BeginUpdate()
                    {
                        _passDirty = false;
                    }
                    public void EndUpdate()
                    {
                        _passDirty = true;
                        /* signal display to recheck dirties */
                        _container._dirtyEvent.Set();
                    }
                    

                    public abstract void Paint(byte[] vram);
                    public abstract void Erase();
                    public abstract void MoveRepaint(byte[] vram);
                }

                public class LabelSprite : Sprite
                {
                    private String _text = "";
                    private Font _font;
                    private Color _color;


                    public LabelSprite(BalanceDisplayModule container, Font font, Color color, int x, int y, int width, int height) : base(container, x, y, width, height, false)
                    {
                        _font = font;
                        _color = color;
                    }


                    int updates = 0;
                    public LabelSprite SetText(String text)
                    {

                        if (updates++ < 5)
                            return this;
                        updates = 0;

                        if (_text.Equals(text))
                        {
                            /* param has not changed, do nothing */
                        }
                        else
                        {
                            _text = text;
                            ForceRefresh();
                        }

                        return this;
                    }
                    public LabelSprite SetColor(Color color)
                    {
                        if (_color == color)
                        {
                            /* param has not changed, do nothing */
                        }
                        else
                        {
                            _color = color;
                            ForceRefresh();
                        }
                        return this;
                    }
                    public LabelSprite SetFont(Font font)
                    {
                        _font = font;

                        ForceRefresh();
                        return this;
                    }
                    public Font Font
                    {
                        get
                        {
                            return _font;
                        }
                    }

                    public override void Paint(byte[] vram)
                    {
                        Microsoft.SPOT.Presentation.Media.Color color2 = (Microsoft.SPOT.Presentation.Media.Color)(_color);

                        Bitmap bmp = AllocBmp();

                        bmp.Clear();
                        bmp.DrawText(_text, _font, color2, 0, 0);

                        byte[] bmpBytes = bmp.GetBitmap();

                        int vramSz = _container.BitmapConverter(bmpBytes, vram);

                        _container.DrawRaw(vram, vramSz, _pos[0], _pos[1], _size[0], _size[1]);
                    }
                    public override void Erase()
                    {
                        int vramSz = _size[0] * _size[1] * 2;
                        _container.DrawRaw_Constant(0x0000, vramSz, _pos[0], _pos[1], _size[0], _size[1]);
                    }
                    public override void MoveRepaint(byte[] vram)
                    {
                        int oldX = _pos[0];
                        int oldY = _pos[1];


                        _pos[0] = _newPos[0];
                        _pos[1] = _newPos[1];


                        Microsoft.SPOT.Presentation.Media.Color color2 = (Microsoft.SPOT.Presentation.Media.Color)(_color);

                        Bitmap bmp = AllocBmp();

                        bmp.Clear();
                        bmp.DrawText(_text, _font, color2, 0, 0);

                        byte[] bmpBytes = bmp.GetBitmap();

                        int vramSz = _container.BitmapConverter(bmpBytes, vram);

                        /* erase */
                        _container.DrawRaw_Constant(0x0000, vramSz, oldX, oldY, _size[0], _size[1]);
                        /* repaint it */
                        _container.DrawRaw(vram, vramSz, _pos[0], _pos[1], _size[0], _size[1]);

                    }
                }

                public class RectSprite : Sprite
                {
                    private Color _color;

                    private ushort _pixelCol;

                    private byte[] pixelBytes = new byte[2];

                    public RectSprite(BalanceDisplayModule container, Color color, int x, int y, int width, int height) : base(container, x, y, width, height, false)
                    {
                        _color = color;
                        _pixelCol = _container.BitmapConvertPixel(color);
                    }

                    public RectSprite SetColor(Color color)
                    {
                        _color = color;
                        _pixelCol = _container.BitmapConvertPixel(color);
                        ForceRefresh();
                        return this;
                    }

                    public override void Paint(byte[] vram)
                    {
                        _container.DrawRaw_Constant(_pixelCol, _size[0] * _size[1] * 2, _pos[0], _pos[1], _size[0], _size[1]);
                    }
                    public override void Erase()
                    {
                    }
                    public override void MoveRepaint(byte[] vram)
                    {
                        //int oldX = _pos[0];
                        //int oldY = _pos[1];
                        //* apply new locs */
                        //_pos[0] = _newPos[0];
                        //_pos[1] = _newPos[1];
                        //* erase */
                        //_container.DrawRaw_Constant(0x0000, _size[0] * _size[1] * 2, oldX, oldY, _size[0], _size[1]);
                        //* repaint it */
                        //_container.DrawRaw_Constant(_pixelCol, _size[0] * _size[1] * 2, _pos[0], _pos[1], _size[0], _size[1]);
                    }
                }

                public class ResourceImageSprite : Sprite
                {

                    System.Resources.ResourceManager _resourceMgr;
                    System.Enum _resourceID;
                    Bitmap.BitmapImageType _imageType;

                    public ResourceImageSprite(BalanceDisplayModule container, int x, int y, System.Resources.ResourceManager resourceMgr, System.Enum resourceID, Bitmap.BitmapImageType imageType) : base(container, x, y)
                    {
                        _resourceMgr = resourceMgr;
                        _resourceID = resourceID;
                        _imageType = imageType;
                    }

                    public override void Paint(byte[] vram)
                    {
                        byte[] btemp = ((byte[])(Microsoft.SPOT.ResourceUtility.GetObject(_resourceMgr, _resourceID)));

                        Bitmap image = new Bitmap(btemp, _imageType);

                        /* save wid/heigh*/
                        _size[0] = image.Width;
                        _size[1] = image.Height;

                        byte[] bmpBytes = image.GetBitmap();

                        int vramSz = _container.BitmapConverter(bmpBytes, vram);

                        _container.DrawRaw(vram, vramSz, _pos[0], _pos[1], _size[0], _size[1]);

                        image.Dispose();
                        image = null;
                        bmpBytes = null;
                    }
                    public override void Erase()
                    {
                        if ((_size[0] == 0) || (_size[1] == 0))
                        {
                            /* nothing to do, we never painted */
                        }
                        else
                        {
                            int vramSz = _size[0] * _size[1] * 2;
                            //ushort pixelCol = 0xFFFF; /* assume white background for now */

                            _container.DrawRaw_Constant(_container.BitmapConvertPixel(_container._backgrndColor), vramSz, _pos[0], _pos[1], _size[0], _size[1]);
                        }
                    }
                    public override void MoveRepaint(byte[] vram)
                    {
                        int oldX = _pos[0];
                        int oldY = _pos[1];

                        _pos[0] = _newPos[0];
                        _pos[1] = _newPos[1];


                        /* paint new one */
                        byte[] btemp = ((byte[])(Microsoft.SPOT.ResourceUtility.GetObject(_resourceMgr, _resourceID)));

                        Bitmap image = new Bitmap(btemp, _imageType);

                        /* save wid/heigh*/
                        _size[0] = image.Width;
                        _size[1] = image.Height;

                        byte[] bmpBytes = image.GetBitmap();

                        int vramSz = _container.BitmapConverter(bmpBytes, vram);

                        //ushort pixelCol = 0xFFFF; /* assume white background for now */

                        /* erase */
                        _container.DrawRaw_Constant(_container.BitmapConvertPixel(_container._backgrndColor), vramSz, oldX, oldY, _size[0], _size[1]);
                        /* paint new one */
                        _container.DrawRaw(vram, vramSz, _pos[0], _pos[1], _size[0], _size[1]);

                        image.Dispose();
                        image = null;
                        bmpBytes = null;
                    }
                }

                /* registers in the display chip */
                private class Registers
                {
                    public const byte ST7735_MADCTL = 0x36;
                    public const byte MADCTL_MY = 0x80;
                    public const byte MADCTL_MX = 0x40;
                    public const byte MADCTL_MV = 0x20;
                    public const byte MADCTL_BGR = 0x08;
                }

                /* managing the graphical obects */
                private Color _backgrndColor = Color.Black; //default to black.
                private ArrayList _sprites = new ArrayList();
                private AutoResetEvent _dirtyEvent = new AutoResetEvent(true);
                private bool _clrScren = false;

                /* pins and spi periph*/
                private OutputPort _outBackLght, _outRs, _outReset;
                private Microsoft.SPOT.Hardware.SPI _spi;

                /* screen orientation */
                private BalanceDisplayModule.OrientationType _orientation;

                /* some cache arrays to optimize the common writes */
                private byte[] _cache_1B;
                private byte[] _cache_manyBs;
                private ushort[] _cache_manyWs;
                private ushort[] _cache_2W;

                /* some Module Information */
                //private ErrorCodeVar status = new ErrorCodeVar();
                private PortDefinition _port;
                private new readonly char kModulePortType = 'S';

				/** set to true to use C# implementation of pixel raster routine */
                public bool UseManagedBitmapConverter { get; set; }

                /** ctor */
                public BalanceDisplayModule(IPortSPI port, OrientationType orientation)
                {
                    if (CTRE.Phoenix.Util.Contains(((PortDefinition)port).types, kModulePortType))
                    {
                        //status = ErrorCode.OK;
                        _port = (PortDefinition)port;

                        bool port1F = port is Port1Definition;
                        SPI.Configuration spiConfiguration;
                        if (port1F)
                        {
                            Port1Definition portDef = (Port1Definition)port;
                            spiConfiguration = new SPI.Configuration(portDef.Pin6, false, 0, 0, false, true, 12000, SPI.SPI_module.SPI4);
                            if (portDef.Pin3 != Cpu.Pin.GPIO_NONE)
                                _outReset = new OutputPort(portDef.Pin3, false);

                            if (portDef.Pin4 != Cpu.Pin.GPIO_NONE)
                                _outBackLght = new OutputPort(portDef.Pin4, true);

                            _outRs = new OutputPort(portDef.Pin5, false);
                        }
                        else
                        {
                            Port8Definition portDef = (Port8Definition)port;
                            spiConfiguration = new SPI.Configuration(portDef.Pin6, false, 0, 0, false, true, 12000, SPI.SPI_module.SPI4);
                            if (portDef.Pin3 != Cpu.Pin.GPIO_NONE)
                                _outReset = new OutputPort(portDef.Pin3, false);

                            if (portDef.Pin4 != Cpu.Pin.GPIO_NONE)
                                _outBackLght = new OutputPort(portDef.Pin4, true);

                            _outRs = new OutputPort(portDef.Pin5, false);
                        }

                        /* alloc the working arrays */
                        _cache_1B = new byte[1];
                        _cache_2W = new ushort[2];
                        _cache_manyBs = new byte[1024];
                        _cache_manyWs = new ushort[1024];

                         _spi = new Microsoft.SPOT.Hardware.SPI(spiConfiguration);

                        /* reset pulse if pin is available */
                        if (_outReset != null)
                        {
                            _outReset.Write(false);
                            Thread.Sleep(150);
                            _outReset.Write(true);
                        }
                        /* setup registers */
                        ConfigureDisplay();

                        /* fixup orientation */
                        _orientation = orientation;
                        ApplytOrientation();

                        /* empty screen */
                        ClearScreen();

                        /* start rendering thread */
                        var th = new Thread(RenderThread);
                        th.Priority = ThreadPriority.Lowest;
                        th.Start();
                    }
                    else
                    {
                        //status = ErrorCode.PORT_MODULE_TYPE_MISMATCH;
                        //CTRE.Phoenix.Reporting.SetError(status);
                    }
                }

                public void SetBackgroundColor(BalanceDisplayModule.Color color)
                {
                    _backgrndColor = color;
                }

                public BalanceDisplayModule.Color GetBackgroundColor()
                {
                    return _backgrndColor;
                }

                private void AddSprite(Sprite sprite)
                {
                    /* add it to coll */
                    lock (_sprites)
                    {
                        _sprites.Add(sprite);
                    }
                    /* signal thread to react */
                    _dirtyEvent.Set();
                }
                public BalanceDisplayModule.LabelSprite AddLabelSprite(Font font, Color color, int x, int y, int width, int height)
                {
                    var retval = new LabelSprite(this, font, color, x, y, width, height);
                    AddSprite(retval);
                    return retval;
                }
                public BalanceDisplayModule.RectSprite AddRectSprite(Color color, int x, int y, int width, int height)
                {
                    var retval = new RectSprite(this, color, x, y, width, height);
                    AddSprite(retval);
                    return retval;
                }
                public BalanceDisplayModule.ResourceImageSprite AddResourceImageSprite(System.Resources.ResourceManager resourceMgr, System.Enum resourceID, Bitmap.BitmapImageType imageType, int x, int y)
                {
                    var retval = new ResourceImageSprite(this, x, y, resourceMgr, resourceID, imageType);
                    AddSprite(retval);
                    return retval;
                }
                public void ClearSprite(Sprite sprite)
                {
                    lock (_sprites)
                    {
                        _sprites.Remove(sprite);
                    }
                }
                public void ClearSprites()
                {
                    lock (_sprites)
                    {
                        _sprites.Clear();
                    }
                }

                private void RenderThread()
                {
                    /* workaround for limited RAM, future firmware release will allow for a larger workspace */
                    byte[] vram = new byte[50 * 100 * 2];
                    // byte[] vram = new byte[DisplayWidth * DisplayHeight * 2];

                    while (true)
                    {
                        if (_dirtyEvent.WaitOne(20, false))
                        {
                            /* did caller want to clear? */
                            if(_clrScren)
                            {
                                _clrScren = false;
                                ClearScreen();
                            }
                            /* loop through sprites and update them */
                            lock (_sprites)
                            {
                                foreach (Sprite sprite in _sprites)
                                {
                                    if (sprite.Dirty)
                                    {
                                        if (sprite.ReadyToMove)
                                        {
                                            sprite.MoveRepaint(vram);
                                        }
                                        else
                                        {
                                            sprite.Paint(vram);
                                        }
                                    }
                                }
                            }
                        }
                    }
                }

                private ushort BitmapConvertPixel(Color color)
                {
                    int raw = (int)color;
                    byte[] arr = new byte[4];
                    arr[0] = (byte)(raw >> 0x00); /* R */
                    arr[1] = (byte)(raw >> 0x08); /* G */
                    arr[2] = (byte)(raw >> 0x10); /* B */

                    byte[] pixelBytes = new byte[2];

                    BitmapConverter(arr, pixelBytes);

                    ushort retval = pixelBytes[0];
                    retval <<= 8;
                    retval |= pixelBytes[1];
                    return retval;
                }
                private int BitmapConverter(byte[] bmp, byte[] pixelBytes)
                {
                    /* pick between native and managed implementation */
                    if (false == UseManagedBitmapConverter)
                    {
                        int er = CTRE.Native.Misc.Bitmap_ConvertBPP(bmp, pixelBytes, 7);    /* implem in native */
                        return bmp.Length / 2;
                    }
                    else
                    {
                        return ManagedBitmap_ConvertBPP(bmp, pixelBytes);             /* implem in managed */
                    }
                }

                private static int ManagedBitmap_ConvertBPP(byte[] bimap, byte[] pixelBytes)
                {
                    Int32 bimapSize = bimap.Length;
                    Int32 outputSize = pixelBytes.Length;

                    int i;
                    UInt16 temp;

                    if ((bimapSize % 4) != 0)
                        return -1;

                    /* Big-endian Blue/Green/Red */
                    if (outputSize < bimapSize / 2)
                        return -3;

                    int outputWordIter = 0;

                    for (i = 0; i < bimapSize; i += 4)
                    {
                        temp = (UInt16)((UInt16)((UInt16)(bimap[i] & 0xF8) << (5 + 6 - 3)) | ((UInt16)(bimap[i + 1] & 0xFC) << 5 - 2) | (bimap[i + 2] >> 3));

                        pixelBytes[outputWordIter] = (byte)(temp >> 8);  // temp[15:8] => first byte
                        pixelBytes[outputWordIter + 1] = (byte)temp;     // temp[7:0] => second byte
                        outputWordIter += 2;
                    }
                    return bimapSize / 2;
                }


                private void DrawRaw(byte[] rawData, int rawDataLen, int x, int y, int width, int height)
                {
                    var orientedWidth = DisplayWidth;
                    var orientedHeight = DisplayHeight;

                    if (_orientation == BalanceDisplayModule.OrientationType.Landscape || 
                        _orientation == BalanceDisplayModule.OrientationType.Landscape_UpsideDown)
                    {
                        orientedWidth = DisplayHeight;
                        orientedHeight = DisplayWidth;
                    }

                    if (x > orientedWidth || y > orientedHeight)
                        return;

                    if (x + width > orientedWidth)
                        width = orientedWidth - x;

                    if (y + height > orientedHeight)
                        height = orientedHeight - y;

                    SetClippingArea(x, y, width - 1, height - 1);
                    WriteCmd(0x2C);
                    Write(rawData, rawDataLen);
                }
          
                private void DrawRaw_Constant(ushort pixelConstant, int rawDataLen, int x, int y, int width, int height)
                {
                    var orientedWidth = DisplayWidth;
                    var orientedHeight = DisplayHeight;

                    if (_orientation == BalanceDisplayModule.OrientationType.Landscape ||
                        _orientation == BalanceDisplayModule.OrientationType.Landscape_UpsideDown)
                    {
                        orientedWidth = DisplayHeight;
                        orientedHeight = DisplayWidth;
                    }

                    if (x > orientedWidth || y > orientedHeight)
                        return;

                    if (x + width > orientedWidth)
                        width = orientedWidth - x;

                    if (y + height > orientedHeight)
                        height = orientedHeight - y;

                    SetClippingArea(x, y, width - 1, height - 1);
                    WriteCmd(0x2C);
                    Write(pixelConstant, rawDataLen);
                }
                
                private void ApplytOrientation()
                {
                    bool isBgr = false;
                    WriteCmd(Registers.ST7735_MADCTL);
                    switch (_orientation)
                    {
                        case BalanceDisplayModule.OrientationType.Portrait: Write((byte)(Registers.MADCTL_MX | Registers.MADCTL_MY | (isBgr ? Registers.MADCTL_BGR : 0))); break;
                        case BalanceDisplayModule.OrientationType.Landscape: Write((byte)(Registers.MADCTL_MV | Registers.MADCTL_MX | (isBgr ? Registers.MADCTL_BGR : 0))); break;
                        case BalanceDisplayModule.OrientationType.Portrait_UpsideDown: Write((byte)(isBgr ? Registers.MADCTL_BGR : 0)); break;
                        case BalanceDisplayModule.OrientationType.Landscape_UpsideDown: Write((byte)(Registers.MADCTL_MV | Registers.MADCTL_MY | (isBgr ? Registers.MADCTL_BGR : 0))); break;
                        default: break;
                    }
                }
                /// <summary>
                /// Clear the screen.
                /// </summary>
                public void Clear()
                {
                    _clrScren = true;
                    _dirtyEvent.Set();
                }
                private void ClearScreen()
                {
                    {
                        ushort pixelColor = BitmapConvertPixel(Color.Black);

                        if (_orientation == BalanceDisplayModule.OrientationType.Portrait ||
                            _orientation == BalanceDisplayModule.OrientationType.Portrait_UpsideDown)
                        {
                            DrawRaw_Constant(pixelColor, 128 * 160 * 2, 0, 0, 128, 160);
                        }
                        else // landscape orients
                        {
                            DrawRaw_Constant(pixelColor, 128 * 160 * 2, 0, 0, 160, 128);
                        }
                    }
                }
                private void ConfigureDisplay()
                {
                    WriteCmd(0x11);//Sleep exit

                    Thread.Sleep(120);

                    //ST7735R Frame Rate
                    WriteCmd(0xB1);
                    Write(0x01); Write(0x2C); Write(0x2D);
                    WriteCmd(0xB2);
                    Write(0x01); Write(0x2C); Write(0x2D);
                    WriteCmd(0xB3);
                    Write(0x01); Write(0x2C); Write(0x2D);
                    Write(0x01); Write(0x2C); Write(0x2D);

                    WriteCmd(0xB4); //Column inversion
                    Write(0x07);

                    //ST7735R Power Sequence
                    WriteCmd(0xC0);
                    Write(0xA2); Write(0x02); Write(0x84);
                    WriteCmd(0xC1); Write(0xC5);
                    WriteCmd(0xC2);
                    Write(0x0A); Write(0x00);
                    WriteCmd(0xC3);
                    Write(0x8A); Write(0x2A);
                    WriteCmd(0xC4);
                    Write(0x8A); Write(0xEE);

                    WriteCmd(0xC5); //VCOM
                    Write(0x0E);

                    WriteCmd(0x36); //MX, MY, RGB mode
                    Write(Registers.MADCTL_MX | Registers.MADCTL_MY | Registers.MADCTL_BGR);

                    //ST7735R Gamma Sequence
                    WriteCmd(0xe0);
                    Write(0x0f); Write(0x1a);
                    Write(0x0f); Write(0x18);
                    Write(0x2f); Write(0x28);
                    Write(0x20); Write(0x22);
                    Write(0x1f); Write(0x1b);
                    Write(0x23); Write(0x37); Write(0x00);

                    Write(0x07);
                    Write(0x02); Write(0x10);
                    WriteCmd(0xe1);
                    Write(0x0f); Write(0x1b);
                    Write(0x0f); Write(0x17);
                    Write(0x33); Write(0x2c);
                    Write(0x29); Write(0x2e);
                    Write(0x30); Write(0x30);
                    Write(0x39); Write(0x3f);
                    Write(0x00); Write(0x07);
                    Write(0x03); Write(0x10);

                    WriteCmd(0x2a);
                    Write(0x00); Write(0x00);
                    Write(0x00); Write(0x7f);
                    WriteCmd(0x2b);
                    Write(0x00); Write(0x00);
                    Write(0x00); Write(0x9f);

                    WriteCmd(0xF0); //Enable test command
                    Write(0x01);
                    WriteCmd(0xF6); //Disable ram power save mode
                    Write(0x00);

                    WriteCmd(0x3A); //65k mode
                    Write(0x05);

                    WriteCmd(0x29); //Display on
                }

                private void SetClippingArea(int x, int y, int width, int height)
                {
                    _cache_2W[0] = (ushort)x;
                    _cache_2W[1] = (ushort)(x + width);
                    WriteCmd(0x2A);
                    Write(_cache_2W);

                    _cache_2W[0] = (ushort)y;
                    _cache_2W[1] = (ushort)(y + height);
                    WriteCmd(0x2B);
                    Write(_cache_2W);
                }
                private void FillArr(ushort[] arr, ushort value)
                {
                    /* if value is zero, use the faster api */
                    if (value == 0)
                    {
                        Array.Clear(arr, 0, arr.Length);
                        return;
                    }

                    /* save len */
                    int arrLen = arr.Length;

                    /* iterator */
                    int i = 0;

                    /* fill up to only four bytes, this is much slower than Array.Copy */
                    int chunkSz = System.Math.Min(arrLen, 4);
                    while (chunkSz-- > 0)
                    {
                        arr[i++] = value; /* fill one byte at a time*/
                    }

                    /* loop until all is filled, performance should be O(log n)  */
                    while (true)
                    {
                        /* how much is left to fill */
                        int remaining = arrLen - i;
                        if (remaining == 0)
                            break;

                        /* copy all bytes from [0,i), over to [i,...), cap to whats left */
                        chunkSz = System.Math.Min(remaining, i);

                        /* copy over already-filled bytes to unfilled portion of arr */
                        Array.Copy(arr, 0, arr, i, chunkSz);
                        i += chunkSz;
                    }
                }
                private void WriteCmd(byte command)
                {
                    _cache_1B[0] = command;

                    _outRs.Write(false);
                    _spi.Write(_cache_1B);
                }
                private void Write(byte data)
                {
                    _cache_1B[0] = data;
                    Write(_cache_1B, 1);
                }
                private void Write(byte[] data, int dataLen)
                {
                    _outRs.Write(true);
                    _spi.WriteRead(data, 0, dataLen, null, 0, 0, 0);
                }
                private void Write(ushort[] data)
                {
                    _outRs.Write(true);
                    _spi.Write(data);
                }
                private void Write(ushort repeatWord, int numBytes)
                {
                    int chunk;
                    /* fill our chunk array */
                    if (_cache_manyWs[0] != repeatWord)
                    {
                        FillArr(_cache_manyWs, repeatWord);
                    }

                    _outRs.Write(true);
                    while (numBytes > 0)
                    {
                        chunk = 2 * _cache_manyWs.Length;
                        if (chunk > numBytes) { chunk = numBytes; }

                        _spi.WriteRead(_cache_manyWs, 0, chunk/2, null, 0, 0, 0);

                        numBytes -= chunk;
                    }
                }
                public bool BacklightEnable
                {
                    set
                    {
                        if (_outBackLght != null)
                            _outBackLght.Write(value);
                    }
                }
                public int DisplayWidth
                {
                    get
                    {
                        return 128;
                    }
                }
                public int DisplayHeight
                {
                    get
                    {
                        return 160;
                    }
                }

            }
        }
    }
}
