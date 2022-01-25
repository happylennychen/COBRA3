using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.ComponentModel;
using System.Collections.ObjectModel;

namespace Cobra.Common
{
    public interface IDEMLib
    {
        void Init(ref BusOptions busoptions, ref ParamListContainer deviceParamlistContainer, ref ParamListContainer sflParamlistContainer);
        void UpdataDEMParameterList(Parameter p);
        bool EnumerateInterface();
        bool CreateInterface();

        UInt32 Erase(ref TASKMessage bgworker);
        UInt32 BlockMap(ref TASKMessage bgworker);
        UInt32 Read(ref TASKMessage bgworker);
        UInt32 Write(ref TASKMessage bgworker);
        UInt32 BitOperation(ref TASKMessage bgworker);
        UInt32 Command(ref TASKMessage bgworker);

        UInt32 ConvertHexToPhysical(ref TASKMessage bgworker);
        UInt32 ConvertPhysicalToHex(ref TASKMessage bgworker);

        UInt32 GetDeviceInfor(ref DeviceInfor deviceinfor);
        UInt32 GetSystemInfor(ref TASKMessage bgworker);
        UInt32 GetRegisteInfor(ref TASKMessage bgworker);
    }

    public interface IDEMLib2 : IDEMLib
    {
        bool DestroyInterface();
        UInt32 ReadDevice(ref TASKMessage msg);
        UInt32 WriteDevice(ref TASKMessage msg);
    }

    public interface IDEMLib3 : IDEMLib2
    {
        UInt32 Verification(ref TASKMessage msg);
    }
}
