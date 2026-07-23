using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace AstroDeviceHub.Ascom
{
    [ComImport, ComVisible(false), InterfaceType(ComInterfaceType.InterfaceIsIUnknown), Guid("00000001-0000-0000-C000-000000000046")]
    internal interface IClassFactory
    {
        void CreateInstance(IntPtr outerUnknown, ref Guid interfaceId, out IntPtr interfacePointer);
        void LockServer(bool lockServer);
    }

    [ComVisible(false)]
    internal sealed class ClassFactory : IClassFactory
    {
        private const uint ClsctxLocalServer = 0x4;
        private const uint RegclsMultipleUse = 0x1;
        private const uint RegclsSuspended = 0x4;
        private static readonly Guid IidUnknown = new Guid("00000000-0000-0000-C000-000000000046");
        private static readonly Guid IidDispatch = new Guid("00020400-0000-0000-C000-000000000046");
        private readonly Type classType;
        private readonly Guid classId;
        private readonly IList<Type> interfaces;
        private uint cookie;

        internal ClassFactory(Type type) { classType = type; classId = Marshal.GenerateGuidForType(type); interfaces = new List<Type>(type.GetInterfaces()); }
        internal void Register() { var id = classId; Marshal.ThrowExceptionForHR(CoRegisterClassObject(ref id, this, ClsctxLocalServer, RegclsMultipleUse | RegclsSuspended, out cookie)); }
        internal void Revoke() { if (cookie != 0) CoRevokeClassObject(cookie); cookie = 0; }
        internal static void ResumeAll() { Marshal.ThrowExceptionForHR(CoResumeClassObjects()); }
        internal static void SuspendAll() { CoSuspendClassObjects(); }
        void IClassFactory.CreateInstance(IntPtr outer, ref Guid iid, out IntPtr pointer)
        {
            pointer = IntPtr.Zero;
            if (outer != IntPtr.Zero) throw new COMException("COM aggregation is unavailable.", unchecked((int)0x80040110));
            var instance = Activator.CreateInstance(classType);
            if (iid == IidDispatch) { pointer = Marshal.GetIDispatchForObject(instance); return; }
            if (iid == IidUnknown) { pointer = Marshal.GetIUnknownForObject(instance); return; }
            foreach (var contract in interfaces) if (iid == Marshal.GenerateGuidForType(contract)) { pointer = Marshal.GetComInterfaceForObject(instance, contract); return; }
            throw new COMException("The requested COM interface is unavailable.", unchecked((int)0x80004002));
        }
        void IClassFactory.LockServer(bool value) { }
        [DllImport("ole32.dll")] private static extern int CoRegisterClassObject(ref Guid clsid, [MarshalAs(UnmanagedType.IUnknown)] object factory, uint context, uint flags, out uint cookie);
        [DllImport("ole32.dll")] private static extern int CoRevokeClassObject(uint cookie);
        [DllImport("ole32.dll")] private static extern int CoResumeClassObjects();
        [DllImport("ole32.dll")] private static extern int CoSuspendClassObjects();
    }
}
