using System;
using System.Runtime.InteropServices;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace POA.PlayerOnePWSDK
{
    public class PlayerOneFilterWheelDLL
    {
        public enum PWErrors              // Return Error Code Definition
        {
            PW_OK = 0,                     // operation successful
            PW_ERROR_INVALID_INDEX,        // invalid index, means the index is < 0 or >= the count
            PW_ERROR_INVALID_HANDLE,       // invalid PW handle
            PW_ERROR_INVALID_ARGU,         // invalid argument(parameter)
            PW_ERROR_NOT_OPENED,           // PW not opened
            PW_ERROR_NOT_FOUND,            // PW not found, may be removed
            PW_ERROR_IS_MOVING,            // PW is moving
            PW_ERROR_POINTER,              // invalid pointer, when get some value, do not pass the NULL pointer to the function
            PW_ERROR_OPERATION_FAILED,     // operation failed (Usually, this is caused by sending commands too often)
            PW_ERROR_FIRMWARE_ERROR,       // firmware error (If you encounter this error, try calling POAResetPW)
        }

        public enum PWState               // PW State Definition
        {
            PW_STATE_CLOSED = 0,           // PW was closed
            PW_STATE_OPENED,               // PW was opened, but not moving(Idle)
            PW_STATE_MOVING                // PW is moving
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct PWProperties              // PW Properties Definition
        {
            [MarshalAs(UnmanagedType.ByValArray, ArraySubType = UnmanagedType.U1, SizeConst = 64)]
            public byte[] PWName;                      // the PW name
            public int Handle;                         // it's unique,PW can be controlled and set by the handle
            public int PositionCount;                  // the filter capacity, eg: 5-position

            [MarshalAs(UnmanagedType.ByValArray, ArraySubType = UnmanagedType.U1, SizeConst = 32)]
            public byte[] serialNumber;                        // the serial number of PW,it's unique

            [MarshalAs(UnmanagedType.ByValArray, ArraySubType = UnmanagedType.U1, SizeConst = 32)]
            public byte[] Reserved;                  // reserved

            public string Name
            {
                get { return Encoding.ASCII.GetString(PWName).TrimEnd((Char)0); }
            }

            public string SN
            {
                get { return Encoding.ASCII.GetString(serialNumber).TrimEnd((Char)0); }
            }
        }

        public const int MAX_FILTER_NAME_LEN = 24; // custom name and filter alias max length

        [DllImport("PlayerOnePW.dll", EntryPoint = "POAGetPWCount", CallingConvention = CallingConvention.Cdecl)]
        private static extern int POAGetPWCount32();

        [DllImport("PlayerOnePW_x64.dll", EntryPoint = "POAGetPWCount", CallingConvention = CallingConvention.Cdecl)]
        private static extern int POAGetPWCount64();


        [DllImport("PlayerOnePW.dll", EntryPoint = "POAGetPWProperties", CallingConvention = CallingConvention.Cdecl)]
        private static extern PWErrors POAGetPWProperties32(int index, out PWProperties pProp);

        [DllImport("PlayerOnePW_x64.dll", EntryPoint = "POAGetPWProperties", CallingConvention = CallingConvention.Cdecl)]
        private static extern PWErrors POAGetPWProperties64(int index, out PWProperties pProp);


        [DllImport("PlayerOnePW.dll", EntryPoint = "POAGetPWPropertiesByHandle", CallingConvention = CallingConvention.Cdecl)]
        private static extern PWErrors POAGetPWPropertiesByHandle32(int Handle, out PWProperties pProp);

        [DllImport("PlayerOnePW_x64.dll", EntryPoint = "POAGetPWPropertiesByHandle", CallingConvention = CallingConvention.Cdecl)]
        private static extern PWErrors POAGetPWPropertiesByHandle64(int Handle, out PWProperties pProp);


        [DllImport("PlayerOnePW.dll", EntryPoint = "POAOpenPW", CallingConvention = CallingConvention.Cdecl)]
        private static extern PWErrors POAOpenPW32(int Handle);

        [DllImport("PlayerOnePW_x64.dll", EntryPoint = "POAOpenPW", CallingConvention = CallingConvention.Cdecl)]
        private static extern PWErrors POAOpenPW64(int Handle);


        [DllImport("PlayerOnePW.dll", EntryPoint = "POAClosePW", CallingConvention = CallingConvention.Cdecl)]
        private static extern PWErrors POAClosePW32(int Handle);

        [DllImport("PlayerOnePW_x64.dll", EntryPoint = "POAClosePW", CallingConvention = CallingConvention.Cdecl)]
        private static extern PWErrors POAClosePW64(int Handle);


        [DllImport("PlayerOnePW.dll", EntryPoint = "POAGetCurrentPosition", CallingConvention = CallingConvention.Cdecl)]
        private static extern PWErrors POAGetCurrentPosition32(int Handle, out int pPosition);

        [DllImport("PlayerOnePW_x64.dll", EntryPoint = "POAGetCurrentPosition", CallingConvention = CallingConvention.Cdecl)]
        private static extern PWErrors POAGetCurrentPosition64(int Handle, out int pPosition);


        [DllImport("PlayerOnePW.dll", EntryPoint = "POAGotoPosition", CallingConvention = CallingConvention.Cdecl)]
        private static extern PWErrors POAGotoPosition32(int Handle, int position);

        [DllImport("PlayerOnePW_x64.dll", EntryPoint = "POAGotoPosition", CallingConvention = CallingConvention.Cdecl)]
        private static extern PWErrors POAGotoPosition64(int Handle, int position);


        [DllImport("PlayerOnePW.dll", EntryPoint = "POAGetOneWay", CallingConvention = CallingConvention.Cdecl)]
        private static extern PWErrors POAGetOneWay32(int Handle, out int pIsOneWay);

        [DllImport("PlayerOnePW_x64.dll", EntryPoint = "POAGetOneWay", CallingConvention = CallingConvention.Cdecl)]
        private static extern PWErrors POAGetOneWay64(int Handle, out int pIsOneWay);


        [DllImport("PlayerOnePW.dll", EntryPoint = "POASetOneWay", CallingConvention = CallingConvention.Cdecl)]
        private static extern PWErrors POASetOneWay32(int Handle, int isOneWay);

        [DllImport("PlayerOnePW_x64.dll", EntryPoint = "POASetOneWay", CallingConvention = CallingConvention.Cdecl)]
        private static extern PWErrors POASetOneWay64(int Handle, int isOneWay);


        [DllImport("PlayerOnePW.dll", EntryPoint = "POAGetPWState", CallingConvention = CallingConvention.Cdecl)]
        private static extern PWErrors POAGetPWState32(int Handle, out PWState pPWState);

        [DllImport("PlayerOnePW_x64.dll", EntryPoint = "POAGetPWState", CallingConvention = CallingConvention.Cdecl)]
        private static extern PWErrors POAGetPWState64(int Handle, out PWState pPWState);


        [DllImport("PlayerOnePW.dll", EntryPoint = "POAGetPWFilterAlias", CallingConvention = CallingConvention.Cdecl)]
        private static extern PWErrors POAGetPWFilterAlias32(int Handle, int position, IntPtr pNameBufOut, int bufLen);

        [DllImport("PlayerOnePW_x64.dll", EntryPoint = "POAGetPWFilterAlias", CallingConvention = CallingConvention.Cdecl)]
        private static extern PWErrors POAGetPWFilterAlias64(int Handle, int position, IntPtr pNameBufOut, int bufLen);


        [DllImport("PlayerOnePW.dll", EntryPoint = "POASetPWFilterAlias", CallingConvention = CallingConvention.Cdecl)]
        private static extern PWErrors POASetPWFilterAlias32(int Handle, int position, IntPtr pNameBufIn, int bufLen);

        [DllImport("PlayerOnePW_x64.dll", EntryPoint = "POASetPWFilterAlias", CallingConvention = CallingConvention.Cdecl)]
        private static extern PWErrors POASetPWFilterAlias64(int Handle, int position, IntPtr pNameBufIn, int bufLen);


        [DllImport("PlayerOnePW.dll", EntryPoint = "POAGetPWFocusOffset", CallingConvention = CallingConvention.Cdecl)]
        private static extern PWErrors POAGetPWFocusOffset32(int Handle, int position, out int pFocusOffsets);

        [DllImport("PlayerOnePW_x64.dll", EntryPoint = "POAGetPWFocusOffset", CallingConvention = CallingConvention.Cdecl)]
        private static extern PWErrors POAGetPWFocusOffset64(int Handle, int position, out int pFocusOffsets);


        [DllImport("PlayerOnePW.dll", EntryPoint = "POASetPWFocusOffset", CallingConvention = CallingConvention.Cdecl)]
        private static extern PWErrors POASetPWFocusOffset32(int Handle, int position, int focusOffsets);

        [DllImport("PlayerOnePW_x64.dll", EntryPoint = "POASetPWFocusOffset", CallingConvention = CallingConvention.Cdecl)]
        private static extern PWErrors POASetPWFocusOffset64(int Handle, int position, int focusOffsets);

        
        [DllImport("PlayerOnePW.dll", EntryPoint = "POAGetPWCustomName", CallingConvention = CallingConvention.Cdecl)]
        private static extern PWErrors POAGetPWCustomName32(int Handle, IntPtr pNameBufOut, int bufLen);

        [DllImport("PlayerOnePW_x64.dll", EntryPoint = "POAGetPWCustomName", CallingConvention = CallingConvention.Cdecl)]
        private static extern PWErrors POAGetPWCustomName64(int Handle, IntPtr pNameBufOut, int bufLen);


        [DllImport("PlayerOnePW.dll", EntryPoint = "POASetPWCustomName", CallingConvention = CallingConvention.Cdecl)]
        private static extern PWErrors POASetPWCustomName32(int Handle, IntPtr pNameBufIn, int bufLen);

        [DllImport("PlayerOnePW_x64.dll", EntryPoint = "POASetPWCustomName", CallingConvention = CallingConvention.Cdecl)]
        private static extern PWErrors POASetPWCustomName64(int Handle, IntPtr pNameBufIn, int bufLen);


        [DllImport("PlayerOnePW.dll", EntryPoint = "POAResetPW", CallingConvention = CallingConvention.Cdecl)]
        private static extern PWErrors POAResetPW32(int Handle);

        [DllImport("PlayerOnePW_x64.dll", EntryPoint = "POAResetPW", CallingConvention = CallingConvention.Cdecl)]
        private static extern PWErrors POAResetPW64(int Handle);


        [DllImport("PlayerOnePW.dll", EntryPoint = "POAGetPWErrorString", CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr POAGetPWErrorString32(PWErrors err);

        [DllImport("PlayerOnePW_x64.dll", EntryPoint = "POAGetPWErrorString", CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr POAGetPWErrorString64(PWErrors err);


        [DllImport("PlayerOnePW.dll", EntryPoint = "POAGetPWAPIVer", CallingConvention = CallingConvention.Cdecl)]
        private static extern int POAGetPWAPIVer32();

        [DllImport("PlayerOnePW_x64.dll", EntryPoint = "POAGetPWAPIVer", CallingConvention = CallingConvention.Cdecl)]
        private static extern int POAGetPWAPIVer64();


        [DllImport("PlayerOnePW.dll", EntryPoint = "POAGetPWSDKVer", CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr POAGetPWSDKVer32();

        [DllImport("PlayerOnePW_x64.dll", EntryPoint = "POAGetPWSDKVer", CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr POAGetPWSDKVer64();


        //define c sharp interface

        public static int POAGetPWCount()
        { return IntPtr.Size == 8 ? POAGetPWCount64() : POAGetPWCount32(); }


        public static PWErrors POAGetPWProperties(int index, out PWProperties pProp)
        { return IntPtr.Size == 8 ? POAGetPWProperties64(index, out pProp) : POAGetPWProperties32(index, out pProp); }


        public static PWErrors POAGetPWPropertiesByHandle(int Handle, out PWProperties pProp)
        { return IntPtr.Size == 8 ? POAGetPWPropertiesByHandle64(Handle, out pProp) : POAGetPWPropertiesByHandle32(Handle, out pProp); }


        public static PWErrors POAOpenPW(int Handle)
        { return IntPtr.Size == 8 ? POAOpenPW64(Handle) : POAOpenPW32(Handle); }


        public static PWErrors POAClosePW(int Handle)
        { return IntPtr.Size == 8 ? POAClosePW64(Handle) : POAClosePW32(Handle); }


        public static PWErrors POAGetCurrentPosition(int Handle, out int pPosition)
        { return IntPtr.Size == 8 ? POAGetCurrentPosition64(Handle, out pPosition) : POAGetCurrentPosition32(Handle, out pPosition); }


        public static PWErrors POAGotoPosition(int Handle, int position)
        { return IntPtr.Size == 8 ? POAGotoPosition64(Handle, position) : POAGotoPosition32(Handle, position); }


        public static PWErrors POAGetOneWay(int Handle, out bool pIsOneWay)
        {
            int isOneway;
            PWErrors error;
            if (IntPtr.Size == 8)
            { error = POAGetOneWay64(Handle, out isOneway); }
            else
            { error = POAGetOneWay32(Handle, out isOneway); }

            pIsOneWay = (isOneway != 0);

            return error;
        }


        public static PWErrors POASetOneWay(int Handle, bool isOneWay)
        { return IntPtr.Size == 8 ? POASetOneWay64(Handle, isOneWay ? 1 : 0) : POASetOneWay32(Handle, isOneWay ? 1 : 0); }


        public static PWErrors POAGetPWState(int Handle, out PWState pPWState)
        { return IntPtr.Size == 8 ? POAGetPWState64(Handle, out pPWState) : POAGetPWState32(Handle, out pPWState); }


        public static PWErrors POAGetPWFilterAlias(int Handle, int position, out string strfilterAlias)
        { 
            int bufLen = MAX_FILTER_NAME_LEN + 8;//more 8 char for the'\0'
            IntPtr pNameBuf = Marshal.AllocCoTaskMem(bufLen); 
            byte[] myByteArray = Enumerable.Repeat((byte)0, bufLen).ToArray();
            Marshal.Copy(myByteArray, 0, pNameBuf, bufLen); // memset to 0

            PWErrors error;
            if (IntPtr.Size == 8)
            {
                error = POAGetPWFilterAlias64(Handle, position, pNameBuf, bufLen);
            }
            else
            {
                error = POAGetPWFilterAlias32(Handle, position, pNameBuf, bufLen);
            }

            strfilterAlias = Marshal.PtrToStringAnsi(pNameBuf);

            Marshal.FreeCoTaskMem(pNameBuf);

            return error;
        }


        public static PWErrors POASetPWFilterAlias(int Handle, int position, string strfilterAlias)
        {
            IntPtr pNameBuf = Marshal.StringToCoTaskMemAnsi(strfilterAlias);
            int bufLen = strfilterAlias.Length;

            PWErrors error;
            if (IntPtr.Size == 8)
            {
                error = POASetPWFilterAlias64(Handle, position, pNameBuf, bufLen);
            }
            else
            {
                error = POASetPWFilterAlias32(Handle, position, pNameBuf, bufLen);
            }

            Marshal.FreeCoTaskMem(pNameBuf);

            return error;
        }

        public static PWErrors POAGetPWFocusOffset(int Handle, int position, out int pFocusOffset)
        { return IntPtr.Size == 8 ? POAGetPWFocusOffset64(Handle, position,  out pFocusOffset) : POAGetPWFocusOffset32(Handle, position, out pFocusOffset); }


        public static PWErrors POASetPWFocusOffset(int Handle, int position, int focusOffset)
        { return IntPtr.Size == 8 ? POASetPWFocusOffset64(Handle, position, focusOffset) : POASetPWFocusOffset32(Handle, position, focusOffset); }


        public static PWErrors POAGetPWCustomName(int Handle, out string strCustomName)
        {
            int bufLen = MAX_FILTER_NAME_LEN + 8;//more 8 char for the'\0'
            IntPtr pNameBuf = Marshal.AllocCoTaskMem(bufLen);
            byte[] myByteArray = Enumerable.Repeat((byte)0, bufLen).ToArray();
            Marshal.Copy(myByteArray, 0, pNameBuf, bufLen); // memset to 0

            PWErrors error;
            if (IntPtr.Size == 8)
            {
                error = POAGetPWCustomName64(Handle, pNameBuf, bufLen);
            }
            else
            {
                error = POAGetPWCustomName32(Handle, pNameBuf, bufLen);
            }

            strCustomName = Marshal.PtrToStringAnsi(pNameBuf);

            Marshal.FreeCoTaskMem(pNameBuf);

            return error;
        }


        public static PWErrors POASetPWCustomName(int Handle, string strCustomName)
        {
            IntPtr pNameBuf = Marshal.StringToCoTaskMemAnsi(strCustomName);
            int bufLen = strCustomName.Length;

            PWErrors error;
            if (IntPtr.Size == 8)
            {
                error = POASetPWCustomName64(Handle, pNameBuf, bufLen);
            }
            else
            {
                error = POASetPWCustomName32(Handle, pNameBuf, bufLen);
            }

            Marshal.FreeCoTaskMem(pNameBuf);

            return error;
        }


        public static PWErrors POAResetPW(int Handle)
        { return IntPtr.Size == 8 ? POAResetPW64(Handle) : POAResetPW32(Handle); }


        public static string POAGetPWErrorString(PWErrors err)
        { return IntPtr.Size == 8 ? Marshal.PtrToStringAnsi(POAGetPWErrorString64(err)) : Marshal.PtrToStringAnsi(POAGetPWErrorString32(err)); }


        public static int POAGetPWAPIVer()
        { return IntPtr.Size == 8 ? POAGetPWAPIVer64() : POAGetPWAPIVer32(); }


        public static string POAGetPWSDKVer()
        { return IntPtr.Size == 8 ? Marshal.PtrToStringAnsi(POAGetPWSDKVer64()) : Marshal.PtrToStringAnsi(POAGetPWSDKVer32()); }
    }
}
