using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Drawing;
using Microsoft.Win32.SafeHandles;
using System.Collections.Specialized;

namespace Cobra.Communication
{

    public static class NativeMethods
    {
        public const UInt32 ERROR_NO_MORE_ITEMS = 0x103;
        public const String KernelDll = "kernel32.dll";

        #region WindowStyle
        [Flags]
        public enum WindowStyle
        {
            WS_OVERLAPPED = 0x00000000,
            WS_POPUP = -2147483648, //0x80000000,
            WS_CHILD = 0x40000000,
            WS_MINIMIZE = 0x20000000,
            WS_VISIBLE = 0x10000000,
            WS_DISABLED = 0x08000000,
            WS_CLIPSIBLINGS = 0x04000000,
            WS_CLIPCHILDREN = 0x02000000,
            WS_MAXIMIZE = 0x01000000,
            WS_CAPTION = 0x00C00000,
            WS_BORDER = 0x00800000,
            WS_DLGFRAME = 0x00400000,
            WS_VSCROLL = 0x00200000,
            WS_HSCROLL = 0x00100000,
            WS_SYSMENU = 0x00080000,
            WS_THICKFRAME = 0x00040000,
            WS_GROUP = 0x00020000,
            WS_TABSTOP = 0x00010000,
            WS_MINIMIZEBOX = 0x00020000,
            WS_MAXIMIZEBOX = 0x00010000,
            WS_TILED = WS_OVERLAPPED,
            WS_ICONIC = WS_MINIMIZE,
            WS_SIZEBOX = WS_THICKFRAME,
            WS_TILEDWINDOW = WS_OVERLAPPEDWINDOW,
            WS_OVERLAPPEDWINDOW = (WS_OVERLAPPED | WS_CAPTION | WS_SYSMENU |
                                    WS_THICKFRAME | WS_MINIMIZEBOX | WS_MAXIMIZEBOX),
            WS_POPUPWINDOW = (WS_POPUP | WS_BORDER | WS_SYSMENU),
            WS_CHILDWINDOW = (WS_CHILD),
//            WS_CUSTOMWINDOW = (WS_CLIPCHILDREN | WS_CAPTION | WS_SYSMENU | WS_THICKFRAME | WS_MINIMIZEBOX | WS_MAXIMIZEBOX)
            WS_CUSTOMWINDOW = (WS_CLIPCHILDREN | WS_SYSMENU | WS_THICKFRAME | WS_MINIMIZEBOX | WS_MAXIMIZEBOX)

        }
        #endregion //WindowStyle

        #region WindowStyleEx
        [Flags]
        public enum WindowStyleEx
        {
            WS_EX_DLGMODALFRAME = 0x00000001,
            WS_EX_NOPARENTNOTIFY = 0x00000004,
            WS_EX_TOPMOST = 0x00000008,
            WS_EX_ACCEPTFILES = 0x00000010,
            WS_EX_TRANSPARENT = 0x00000020,
            WS_EX_MDICHILD = 0x00000040,
            WS_EX_TOOLWINDOW = 0x00000080,
            WS_EX_WINDOWEDGE = 0x00000100,
            WS_EX_CLIENTEDGE = 0x00000200,
            WS_EX_CONTEXTHELP = 0x00000400,
            WS_EX_RIGHT = 0x00001000,
            WS_EX_LEFT = 0x00000000,
            WS_EX_RTLREADING = 0x00002000,
            WS_EX_LTRREADING = 0x00000000,
            WS_EX_LEFTSCROLLBAR = 0x00004000,
            WS_EX_RIGHTSCROLLBAR = 0x00000000,
            WS_EX_CONTROLPARENT = 0x00010000,
            WS_EX_STATICEDGE = 0x00020000,
            WS_EX_APPWINDOW = 0x00040000,
            WS_EX_OVERLAPPEDWINDOW = (WS_EX_WINDOWEDGE | WS_EX_CLIENTEDGE),
            WS_EX_PALETTEWINDOW = (WS_EX_WINDOWEDGE | WS_EX_TOOLWINDOW | WS_EX_TOPMOST),
            WS_EX_LAYERED = 0x00080000,
            WS_EX_NOINHERITLAYOUT = 0x00100000, // Disable inheritence of mirroring by children
            WS_EX_LAYOUTRTL = 0x00400000, // Right to left mirroring
            WS_EX_COMPOSITED = 0x02000000,
            WS_EX_NOACTIVATE = 0x08000000,
        }
        #endregion //WindowStyleEx

        #region WindowMessages
        public enum WindowMessages
        {
            WM_NULL = 0x0000,
            WM_CREATE = 0x0001,
            WM_DESTROY = 0x0002,
            WM_MOVE = 0x0003,
            WM_SIZE = 0x0005,
            WM_ACTIVATE = 0x0006,
            WM_SETFOCUS = 0x0007,
            WM_KILLFOCUS = 0x0008,
            WM_ENABLE = 0x000A,
            WM_SETREDRAW = 0x000B,
            WM_SETTEXT = 0x000C,
            WM_GETTEXT = 0x000D,
            WM_GETTEXTLENGTH = 0x000E,
            WM_PAINT = 0x000F,
            WM_CLOSE = 0x0010,

            WM_QUIT = 0x0012,
            WM_ERASEBKGND = 0x0014,
            WM_SYSCOLORCHANGE = 0x0015,
            WM_SHOWWINDOW = 0x0018,

            WM_ACTIVATEAPP = 0x001C,

            WM_SETCURSOR = 0x0020,
            WM_MOUSEACTIVATE = 0x0021,
            WM_GETMINMAXINFO = 0x24,
            WM_WINDOWPOSCHANGING = 0x0046,
            WM_WINDOWPOSCHANGED = 0x0047,

            WM_CONTEXTMENU = 0x007B,
            WM_STYLECHANGING = 0x007C,
            WM_STYLECHANGED = 0x007D,
            WM_DISPLAYCHANGE = 0x007E,
            WM_GETICON = 0x007F,
            WM_SETICON = 0x0080,

            // non client area
            WM_NCCREATE = 0x0081,
            WM_NCDESTROY = 0x0082,
            WM_NCCALCSIZE = 0x0083,
            WM_NCHITTEST = 0x84,
            WM_NCPAINT = 0x0085,
            WM_NCACTIVATE = 0x0086,

            WM_GETDLGCODE = 0x0087,

            WM_SYNCPAINT = 0x0088,

            // non client mouse
            
            WM_NCMOUSEMOVE = 0x00A0,
            WM_NCLBUTTONDOWN = 0x00A1,
            WM_NCLBUTTONUP = 0x00A2,
            WM_NCLBUTTONDBLCLK = 0x00A3,
            WM_NCRBUTTONDOWN = 0x00A4,
            WM_NCRBUTTONUP = 0x00A5,
            WM_NCRBUTTONDBLCLK = 0x00A6,
            WM_NCMBUTTONDOWN = 0x00A7,
            WM_NCMBUTTONUP = 0x00A8,
            WM_NCMBUTTONDBLCLK = 0x00A9,

            // keyboard
            WM_KEYDOWN = 0x0100,
            WM_KEYUP = 0x0101,
            WM_CHAR = 0x0102,

            WM_SYSCOMMAND = 0x0112,

            // menu
            WM_INITMENU = 0x0116,
            WM_INITMENUPOPUP = 0x0117,
            WM_MENUSELECT = 0x011F,
            WM_MENUCHAR = 0x0120,
            WM_ENTERIDLE = 0x0121,
            WM_MENURBUTTONUP = 0x0122,
            WM_MENUDRAG = 0x0123,
            WM_MENUGETOBJECT = 0x0124,
            WM_UNINITMENUPOPUP = 0x0125,
            WM_MENUCOMMAND = 0x0126,

            WM_CHANGEUISTATE = 0x0127,
            WM_UPDATEUISTATE = 0x0128,
            WM_QUERYUISTATE = 0x0129,

            // mouse
            WM_MOUSEFIRST = 0x0200,
            WM_MOUSEMOVE = 0x0200,
            WM_LBUTTONDOWN = 0x0201,
            WM_LBUTTONUP = 0x0202,
            WM_LBUTTONDBLCLK = 0x0203,
            WM_RBUTTONDOWN = 0x0204,
            WM_RBUTTONUP = 0x0205,
            WM_RBUTTONDBLCLK = 0x0206,
            WM_MBUTTONDOWN = 0x0207,
            WM_MBUTTONUP = 0x0208,
            WM_MBUTTONDBLCLK = 0x0209,
            WM_MOUSEWHEEL = 0x020A,
            WM_MOUSELAST = 0x020D,

            WM_PARENTNOTIFY = 0x0210,
            WM_ENTERMENULOOP = 0x0211,
            WM_EXITMENULOOP = 0x0212,

            WM_NEXTMENU = 0x0213,
            WM_SIZING = 0x0214,
            WM_CAPTURECHANGED = 0x0215,
            WM_MOVING = 0x0216,

            WM_ENTERSIZEMOVE = 0x0231,
            WM_EXITSIZEMOVE = 0x0232,

            WM_MOUSELEAVE = 0x02A3,
            WM_MOUSEHOVER = 0x02A1,
            WM_NCMOUSEHOVER = 0x02A0,
            WM_NCMOUSELEAVE = 0x02A2,

            WM_MDIACTIVATE = 0x0222,
            WM_HSCROLL = 0x0114,
            WM_VSCROLL = 0x0115,

            WM_PRINT = 0x0317,
            WM_PRINTCLIENT = 0x0318,
        }
        #endregion //WindowMessages

        #region DCX enum
        [Flags]
        public enum DCX
        {
            DCX_CACHE = 0x2,
            DCX_CLIPCHILDREN = 0x8,
            DCX_CLIPSIBLINGS = 0x10,
            DCX_EXCLUDERGN = 0x40,
            DCX_EXCLUDEUPDATE = 0x100,
            DCX_INTERSECTRGN = 0x80,
            DCX_INTERSECTUPDATE = 0x200,
            DCX_LOCKWINDOWUPDATE = 0x400,
            DCX_NORECOMPUTE = 0x100000,
            DCX_NORESETATTRS = 0x4,
            DCX_PARENTCLIP = 0x20,
            DCX_VALIDATE = 0x200000,
            DCX_WINDOW = 0x1,
        }
        #endregion //DCX

        [Flags]
        public enum ClassDevsFlags
        {
            DIGCF_DEFAULT = 0x00000001,
            DIGCF_PRESENT = 0x00000002,
            DIGCF_ALLCLASSES = 0x00000004,
            DIGCF_PROFILE = 0x00000008,
            DIGCF_DEVICEINTERFACE = 0x00000010,
        }

        public enum RegPropertyType
        {
            SPDRP_DEVICEDESC = 0x00000000, // DeviceDesc (R/W)
            SPDRP_HARDWAREID = 0x00000001, // HardwareID (R/W)
            SPDRP_COMPATIBLEIDS = 0x00000002, // CompatibleIDs (R/W)
            SPDRP_UNUSED0 = 0x00000003, // unused
            SPDRP_SERVICE = 0x00000004, // Service (R/W)
            SPDRP_UNUSED1 = 0x00000005, // unused
            SPDRP_UNUSED2 = 0x00000006, // unused
            SPDRP_CLASS = 0x00000007, // Class (R--tied to ClassGUID)
            SPDRP_CLASSGUID = 0x00000008, // ClassGUID (R/W)
            SPDRP_DRIVER = 0x00000009, // Driver (R/W)
            SPDRP_CONFIGFLAGS = 0x0000000A, // ConfigFlags (R/W)
            SPDRP_MFG = 0x0000000B, // Mfg (R/W)
            SPDRP_FRIENDLYNAME = 0x0000000C, // FriendlyName (R/W)
            SPDRP_LOCATION_INFORMATION = 0x0000000D,// LocationInformation (R/W)
            SPDRP_PHYSICAL_DEVICE_OBJECT_NAME = 0x0000000E, // PhysicalDeviceObjectName (R)
            SPDRP_CAPABILITIES = 0x0000000F, // Capabilities (R)
            SPDRP_UI_NUMBER = 0x00000010, // UiNumber (R)
            SPDRP_UPPERFILTERS = 0x00000011, // UpperFilters (R/W)
            SPDRP_LOWERFILTERS = 0x00000012, // LowerFilters (R/W)
            SPDRP_BUSTYPEGUID = 0x00000013, // BusTypeGUID (R)
            SPDRP_LEGACYBUSTYPE = 0x00000014, // LegacyBusType (R)
            SPDRP_BUSNUMBER = 0x00000015, // BusNumber (R)
            SPDRP_ENUMERATOR_NAME = 0x00000016, // Enumerator Name (R)
            SPDRP_SECURITY = 0x00000017, // Security (R/W, binary form)
            SPDRP_SECURITY_SDS = 0x00000018, // Security (W, SDS form)
            SPDRP_DEVTYPE = 0x00000019, // Device Type (R/W)
            SPDRP_EXCLUSIVE = 0x0000001A, // Device is exclusive-access (R/W)
            SPDRP_CHARACTERISTICS = 0x0000001B, // Device Characteristics (R/W)
            SPDRP_ADDRESS = 0x0000001C, // Device Address (R)
            SPDRP_UI_NUMBER_DESC_FORMAT = 0x0000001E, // UiNumberDescFormat (R/W)
            SPDRP_MAXIMUM_PROPERTY = 0x0000001F  // Upper bound on ordinals
        }

        public const int WM_DEVICECHANGE = 0x0219;
        public const int DBT_DEVNODES_CHANGED = 7; // logical volume
        public const int DEVICE_NOTIFY_WINDOW_HANDLE = 0;
        public const int DBT_DEVTYP_DEVICEINTERFACE = 5;
        public const int DBT_DEVICEARRIVAL = 0x8000;
        public const int DBT_DEVICEQUERYREMOVE = 0x8001;
        public const int DBT_DEVICEQUERYREMOVEFAILED = 0x8002;
        public const int DBT_DEVICEREMOVEPENDING = 0x8003;
        public const int DBT_DEVICEREMOVECOMPLETE = 0x8004;

        public const uint GENERIC_READ = 0x80000000;
        public const uint GENERIC_WRITE = 0x40000000;
        public const uint FILE_SHARE_READ = 0x00000001;
        public const uint FILE_SHARE_WRITE = 0x00000002;

        public const uint FILE_FLAG_OVERLAPPED = 0x40000000;
        public const uint FILE_FLAG_NO_BUFFERING = 0x20000000;

        public const int OPEN_EXISTING = 3;
        public const int INVALID_HANDLE_VALUE = -1;

        public const int PURGE_RXABORT = 0x0002;
        public const int PURGE_RXCLEAR = 0x0008;
        public const int PURGE_TXABORT = 0x0001;
        public const int PURGE_TXCLEAR = 0x0004;

        #region RECT structure

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            private readonly int left;
            private readonly int top;
            private readonly int right;
            private readonly int bottom;

            private RECT(int left, int top, int right, int bottom)
            {
                this.left = left;
                this.top = top;
                this.right = right;
                this.bottom = bottom;
            }

            public Rectangle Rect { get { return new Rectangle(left, top, right - left, bottom - top); } }

/*
            public static RECT FromXYWH(int x, int y, int width, int height)
            {
                return new RECT(x,
                                y,
                                x + width,
                                y + height);
            }
*/

            public static RECT FromRectangle(Rectangle rect)
            {
                return new RECT(rect.Left,
                                 rect.Top,
                                 rect.Right,
                                 rect.Bottom);
            }
        }

        #endregion RECT structure

        #region WINDOWPOS
        [StructLayout(LayoutKind.Sequential)]
        public struct WINDOWPOS
        {
            private IntPtr hwnd;
            private IntPtr hWndInsertAfter;
            public int x;
            public int y;
            public int cx;
            public int cy;
            public uint flags;
        }
        #endregion //WINDOWPOS

        #region NCCALCSIZE_PARAMS
        [StructLayout(LayoutKind.Sequential)]
        public struct NCCALCSIZE_PARAMS
        {
            
            public RECT rectProposed;
            
            public RECT rectBeforeMove;
            
            public RECT rectClientBeforeMove;
            
            public WINDOWPOS lpPos;
        }
        #endregion //NCCALCSIZE_PARAMS

/*        [DllImport("user32.dll")]
        public static extern IntPtr GetDCEx(IntPtr hwnd, IntPtr hrgnclip, uint fdwOptions);

        [DllImport("user32.dll")]
        public static extern int ReleaseDC(IntPtr hwnd, IntPtr hDC);
        */
        [DllImportAttribute("user32.dll")]
        public static extern int SendMessage(IntPtr hWnd, int Msg, int wParam, int lParam);
        [DllImportAttribute("user32.dll")]
        public static extern Int32 ReleaseCapture();

        [DllImport(KernelDll)]
        public static extern IntPtr CreateFile(
            string lpFileName,
            UInt32 dwDesiredAccess,
            UInt32 dwShareMode,
            int lpSecurityAttributes,
            UInt32 dwCreationDisposition,
            UInt32 dwFlagsAndAttributes,
            int hTemplateFile
            );

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        internal static extern SafeFileHandle CreateFile(
            String lpFileName, 
            UInt32 dwDesiredAccess, 
            UInt32 dwShareMode, 
            IntPtr lpSecurityAttributes,
            UInt32 dwCreationDisposition,
            UInt32 dwFlagsAndAttributes, 
            int hTemplateFile);


   /*     [DllImport(KernelDll)]
        public static extern unsafe Int32 ReadFile(
            IntPtr hFile,
            [MarshalAs(UnmanagedType.LPArray)] byte[] lpBuffer,
            UInt32 nNumberOfBytesToRead,
            int* lpNumberOfBytesRead,
            ref OVERLAPPED lpOverlapped
        );
        [DllImport(KernelDll)]
        public static extern unsafe Int32 WriteFile(
            IntPtr hFile,
            [MarshalAs(UnmanagedType.LPArray)] byte[] lpBuffer,
            UInt32 nNumberOfBytesToWrite,
            int* lpNumberOfBytesWritten,
            ref OVERLAPPED lpOverlapped
        );*/
//        [DllImport(KernelDll)]
  //      public static extern Int32 CloseHandle(IntPtr hObject);

//        [DllImport(KernelDll)]
  //      public static extern UInt32 WaitForSingleObject(IntPtr hHandle, UInt32 dwMilliseconds);

//        [DllImport(KernelDll)]
  //      public static extern unsafe IntPtr CreateEvent(int* lpEventAttributes, Int32 bManualReset, Int32 bInitialState, string lpName);

//        [DllImport(KernelDll)]
  //      public static extern int ResetEvent(IntPtr hHandle);

//        [DllImport(KernelDll)]
  //      public static extern Int32 GetOverlappedResult(IntPtr hFile, ref OVERLAPPED lpOverlapped, ref UInt32 lpNumberOfBytesTransferred, Int32 bWait);

        [DllImport(KernelDll)]
        public static extern UInt32 GetLastError();


/*
        [DllImport("hid.dll", SetLastError=true)]
        public static extern void HidD_GetHidGuid(
			ref Guid lpHidGuid
			);
*/

		 
		[DllImport("setupapi.dll", SetLastError=true)]
        public static extern unsafe int SetupDiGetClassDevs(
			ref Guid  lpGuid,
			int*	  Enumerator,
			int*      hwndParent,
			ClassDevsFlags  Flags
			);

/*
		[DllImport("setupapi.dll", SetLastError=true)]
        public static extern unsafe int SetupDiGetClassDevs(
			int*  guid,
			int*  Enumerator,
			int*  hwndParent,
			ClassDevsFlags  Flags
			);
*/
		[DllImport("setupapi.dll", SetLastError = true)]
		public static extern unsafe bool SetupDiDestroyDeviceInfoList(
			int DeviceInfoSet
		);


		[StructLayout(LayoutKind.Sequential)]
        public struct SP_DEVINFO_DATA
		{
            public UInt32  cbSize;
			public Guid    ClassGuid;
			public UInt32  DevInst;
            public IntPtr Reserved;
		}


/*
		[DllImport("setupapi.dll", SetLastError=true)]
        public static extern unsafe int SetupDiEnumDeviceInfo(
			int  DeviceInfoSet,
			int	 Index, 
			ref  SP_DEVINFO_DATA DeviceInfoData
			);
*/



		// Device interface data
		[StructLayout(LayoutKind.Sequential)]
			public struct SP_DEVICE_INTERFACE_DATA 
		{
			public UInt32 cbSize;
			public Guid InterfaceClassGuid;
			public UInt32 Flags;
			public IntPtr Reserved;
		}

		// Device interface detail data
		[StructLayout(LayoutKind.Sequential, CharSet= CharSet.Ansi)]
		public struct PSP_DEVICE_INTERFACE_DETAIL_DATA
		{
            public UInt32 cbSize;
			[MarshalAs(UnmanagedType.ByValTStr, SizeConst= 255)]
			public string DevicePath;
		}


		[DllImport("setupapi.dll", SetLastError=true)]
		public static extern bool SetupDiEnumDeviceInterfaces(
			int  DeviceInfoSet,
			int  DeviceInfoData,
			ref  Guid  lpHidGuid,
			int  MemberIndex,
			ref  SP_DEVICE_INTERFACE_DATA lpDeviceInterfaceData);


		[DllImport("setupapi.dll", SetLastError=true)]
		public static extern  unsafe int SetupDiGetDeviceInterfaceDetail(
			int  DeviceInfoSet,
			ref SP_DEVICE_INTERFACE_DATA lpDeviceInterfaceData,
			int* aPtr,
			int detailSize,
			ref int requiredSize,
			int* bPtr);
            //ref SP_DEVINFO_DATA DeviceInfoData);



        [DllImport("setupapi.dll", SetLastError = true)]
        public static extern int SetupDiGetDeviceInterfaceDetail(
            int DeviceInfoSet,
            ref SP_DEVICE_INTERFACE_DATA lpDeviceInterfaceData,
            ref PSP_DEVICE_INTERFACE_DETAIL_DATA myPSP_DEVICE_INTERFACE_DETAIL_DATA,
            int detailSize,
            ref int requiredSize,
            //int* bPtr);
			ref SP_DEVINFO_DATA DeviceInfoData);

/*
		[DllImport("setupapi.dll", SetLastError=true)]
        public static extern unsafe int SetupDiGetDeviceRegistryProperty(
			int					DeviceInfoSet,
			ref SP_DEVINFO_DATA DeviceInfoData,
			RegPropertyType		Property, 
			int*				PropertyRegDataType, 
			int*				PropertyBuffer, 
			int					PropertyBufferSize, 
			ref int				RequiredSize
		);
*/

		// Device interface detail data
		[StructLayout(LayoutKind.Sequential, CharSet= CharSet.Ansi)]
		public struct DATA_BUFFER
		{
			[MarshalAs(UnmanagedType.ByValTStr, SizeConst= 1024)]
			public string Buffer;
		}

		[DllImport("setupapi.dll", SetLastError=true)]
        public static extern unsafe int SetupDiGetDeviceRegistryProperty(
			int					DeviceInfoSet,
			ref SP_DEVINFO_DATA DeviceInfoData,
			RegPropertyType		Property, 
			int*				PropertyRegDataType, 
			ref DATA_BUFFER		PropertyBuffer, 
			int					PropertyBufferSize, 
			ref int				RequiredSize
		);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern IntPtr RegisterDeviceNotification(IntPtr hRecipient,
           ref DEV_BROADCAST_DEVICEINTERFACE NotificationFilter, uint Flags);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern Int32 UnregisterDeviceNotification(IntPtr Handle);


        [StructLayout(LayoutKind.Sequential)]
        public struct DEV_BROADCAST_DEVICEINTERFACE
        {
            public int dbcc_size;  
            public int dbcc_devicetype;  
            public int dbcc_reserved;  
            public Guid  dbcc_classguid;  
			[MarshalAs(UnmanagedType.ByValArray, SizeConst= 255)]
			public Char[] dbcc_name;
        }


        public static IntPtr [] RegisterHwInterfaces(IntPtr Handle, Guid [] guids)
        {
            if (guids == null) return null;
            List<IntPtr> aList = new List<IntPtr>();
            foreach (Guid aGuid in guids)
            {

                DEV_BROADCAST_DEVICEINTERFACE NotificationFilter = new DEV_BROADCAST_DEVICEINTERFACE();
                NotificationFilter.dbcc_size = Marshal.SizeOf(NotificationFilter);
                NotificationFilter.dbcc_devicetype = DBT_DEVTYP_DEVICEINTERFACE;
                NotificationFilter.dbcc_classguid = aGuid;
                NotificationFilter.dbcc_reserved = 0;
                aList.Add(RegisterDeviceNotification(Handle, ref NotificationFilter, DEVICE_NOTIFY_WINDOW_HANDLE));
            }
            return aList.ToArray();
        }

		//(A130521)
		public static bool RegisterForDevChange(IntPtr PtrHandle, Guid aGuid, ref IntPtr hNotifyDevNode)
		{
			if (PtrHandle == null) return false;
			if (aGuid == null) return false;

			DEV_BROADCAST_DEVICEINTERFACE NotificationFilter = new DEV_BROADCAST_DEVICEINTERFACE();
			NotificationFilter.dbcc_size = Marshal.SizeOf(NotificationFilter);
			NotificationFilter.dbcc_devicetype = DBT_DEVTYP_DEVICEINTERFACE;
			NotificationFilter.dbcc_classguid = aGuid;
			NotificationFilter.dbcc_reserved = 0;
			hNotifyDevNode = RegisterDeviceNotification(PtrHandle, ref NotificationFilter, DEVICE_NOTIFY_WINDOW_HANDLE);

			if (hNotifyDevNode != null)
				return true;
			else
				return false;
		}
		//(E130521)

        public static void UnregisterHwInterfaces(IntPtr[] handles)
        {
            if (handles == null) return;
            foreach (IntPtr intPtr in handles)
            {
                UnregisterDeviceNotification(intPtr);
            }
        }

/*
        public static IntPtr RegisterI2CUSBInterface(IntPtr Handle)
        {
            DEV_BROADCAST_DEVICEINTERFACE NotificationFilter = new DEV_BROADCAST_DEVICEINTERFACE();
            NotificationFilter.dbcc_size = Marshal.SizeOf(NotificationFilter);
            NotificationFilter.dbcc_devicetype = DBT_DEVTYP_DEVICEINTERFACE;
            return RegisterDeviceNotification(Handle, ref NotificationFilter, DEVICE_NOTIFY_WINDOW_HANDLE);
        }
*/

/*
        public static unsafe bool UnregisterI2CUSBInterface(IntPtr Handle)
        { 
            return UnregisterDeviceNotification(Handle);
        }
*/

        public static DEV_BROADCAST_DEVICEINTERFACE PtrToDevInfo(IntPtr ptr)
        {
            return (DEV_BROADCAST_DEVICEINTERFACE)Marshal.PtrToStructure(ptr, typeof(DEV_BROADCAST_DEVICEINTERFACE));
        }

        // P/Invoke:
        private const uint CTLCODE = 0xdaf52480;
//        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
//        private static extern IntPtr CreateFile(string filename, FileAccess access,
//              FileShare sharing, IntPtr SecurityAttributes, FileMode mode,
//              FileOptions options, IntPtr template
//        );
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool DeviceIoControl(IntPtr device, uint ctlcode,
            ref byte inbuffer, int inbuffersize,
            IntPtr outbuffer, int outbufferSize,
            IntPtr bytesreturned, IntPtr overlapped
        );

        /*
        [DllImport("kernel32", SetLastError = true, ThrowOnUnmappableChar = true, CharSet = CharSet.Unicode)]
        static extern unsafe IntPtr CreateFile
        (
            string FileName,          // file name
            uint DesiredAccess,       // access mode
            uint ShareMode,           // share mode
            uint SecurityAttributes,  // Security Attributes
            uint CreationDisposition, // how to create
            uint FlagsAndAttributes,  // file attributes
            int hTemplateFile         // handle to template file
        );//        [DllImport("kernel32.dll")]

        [DllImport("kernel32", SetLastError = true)]
        static extern unsafe bool ReadFile
        (
            IntPtr hFile,      // handle to file
            void* pBuffer,            // data buffer
            int NumberOfBytesToRead,  // number of bytes to read
            int* pNumberOfBytesRead,  // number of bytes read
            int Overlapped            // overlapped buffer
        );

        [DllImport("kernel32", SetLastError = true)]
        static extern unsafe bool CloseHandle
        (
            System.IntPtr hObject // handle to object
        );
        */

        [DllImport("kernel32", SetLastError = true, ThrowOnUnmappableChar = true, CharSet = CharSet.Unicode)]
        public static extern IntPtr CreateFile(
            string lpFileName,                         // file name
            uint dwDesiredAccess,                      // access mode
            int dwShareMode,                          // share mode
            int lpSecurityAttributes, // SD
            int dwCreationDisposition,                // how to create
            int dwFlagsAndAttributes,                 // file attributes
            int hTemplateFile                        // handle to template file
            );

        [DllImport("kernel32", SetLastError = true)]
        public static extern bool ReadFile(
            IntPtr hFile,                // handle to file
            byte[] lpBuffer,             // data buffer
            int nNumberOfBytesToRead,  // number of bytes to read
            ref int lpNumberOfBytesRead, // number of bytes read
            ref OVERLAPPED lpOverlapped    // overlapped buffer
            );

        [DllImport("kernel32", SetLastError = true)]
        public static extern bool WriteFile(
            IntPtr hFile,                    // handle to file
            byte[] lpBuffer,                // data buffer
            int nNumberOfBytesToWrite,     // number of bytes to write
            ref int lpNumberOfBytesWritten,  // number of bytes written
            ref OVERLAPPED lpOverlapped        // overlapped buffer
            );

        [DllImport("kernel32", SetLastError = true)]
        public static extern bool CloseHandle(
            IntPtr hObject   // handle to object
            );

        [DllImport("kernel32", SetLastError = true)]
        public static extern bool GetCommState(
            IntPtr hFile,  // handle to communications device
            ref DCB lpDCB    // device-control block
            );
        [DllImport("kernel32", SetLastError = true)]
        public static extern bool BuildCommDCB(
            string lpDef,  // device-control string
            ref DCB lpDCB     // device-control block
            );
        [DllImport("kernel32", SetLastError = true)]
        public static extern bool SetCommState(
            IntPtr hFile,  // handle to communications device
            ref DCB lpDCB    // device-control block
            );
        [DllImport("kernel32", SetLastError = true)]
        public static extern bool GetCommTimeouts(
            IntPtr hFile,                  // handle to comm device
            ref COMMTIMEOUTS lpCommTimeouts  // time-out values
            );
        [DllImport("kernel32", SetLastError = true)]
        public static extern bool SetCommTimeouts(
            IntPtr hFile,                  // handle to comm device
            ref COMMTIMEOUTS lpCommTimeouts  // time-out values
            );

        [DllImport("kernel32", SetLastError = true)]
        public static extern bool PurgeComm(
            IntPtr hFile,
            UInt32 dwFlags
        );

        //(M180817)Francis, issueid=1113, try to solve slow download speed
        [StructLayout(LayoutKind.Sequential)]
        public struct DCB
        {
            //taken from c struct in platform sdk 
            public UInt32 DCBlength;           // sizeof(DCB) 
            public UInt32 BaudRate;            // current baud rate 
            //public UInt32 fBinary;
            //public UInt32 fBinary;          // binary mode, no EOF check 
            //public UInt32 fParity;          // enable parity checking 
            //public UInt32 fOutxCtsFlow;      // CTS output flow control 
            //public UInt32 fOutxDsrFlow;      // DSR output flow control 
            //public UInt32 fDtrControl;       // DTR flow control type 
            //public UInt32 fDsrSensitivity;   // DSR sensitivity 
            //public UInt32 fTXContinueOnXoff; // XOFF continues Tx 
            //public UInt32 fOutX;          // XON/XOFF out flow control 
            //public UInt32 fInX;           // XON/XOFF in flow control 
            //public UInt32 fErrorChar;     // enable error replacement 
            //public UInt32 fNull;          // enable null stripping 
            //public UInt32 fRtsControl;     // RTS flow control 
            //public UInt32 fAbortOnError;   // abort on error 
            //public UInt32 fDummy2;        // reserved 
            public UInt32 flags;
            public UInt16 wReserved;          // not currently used 
            public UInt16 XonLim;             // transmit XON threshold 
            public UInt16 XoffLim;            // transmit XOFF threshold 
            public Byte ByteSize;           // number of bits/byte, 4-8 
            public Byte Parity;             // 0-4=no,odd,even,mark,space 
            public Byte StopBits;           // 0,1,2 = 1, 1.5, 2 
            public Char XonChar;            // Tx and Rx XON character 
            public Char XoffChar;           // Tx and Rx XOFF character 
            public Char ErrorChar;          // error replacement character 
            public Char EofChar;            // end of input character 
            public Char EvtChar;            // received event character 
            public UInt16 wReserved1;         // reserved; do not use 
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct COMMTIMEOUTS
        {
            public int ReadIntervalTimeout;
            public int ReadTotalTimeoutMultiplier;
            public int ReadTotalTimeoutConstant;
            public int WriteTotalTimeoutMultiplier;
            public int WriteTotalTimeoutConstant;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct OVERLAPPED
        {
            public int Internal;
            public int InternalHigh;
            public int Offset;
            public int OffsetHigh;
            public int hEvent;
        }

//        private static extern void CloseHandle(IntPtr hdl);
    }

}
