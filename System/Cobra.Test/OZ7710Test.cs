using O2Micro.Cobra.Common;
using O2Micro.Cobra.KALL10;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace O2Micro.Cobra.Test
{
    public class OZ7710Test
    {
        [Fact]
        public void CheckBinDataShouldWork()
        {
            //Arrange
            UInt32 expected = LibErrorCode.IDS_ERR_SUCCESSFUL;
            var list = new byte[]{
                0x66, 0x42, 0x11,
                0x67, 0xAD, 0x95,
                0x68, 0xF1, 0xC8,
                0x69, 0x94, 0xC8,
                0x6A, 0xA1, 0x50,
                0x6B, 0x72, 0xD0,
                0x6C, 0x29, 0x9B,
                0x6D, 0x27, 0xBC,
                0x6E, 0x24, 0x45,
                0x6F, 0x81, 0xAE
            };
            List<byte> blist = new List<byte>(list);
            DEMBehaviorManage behaviorManage = new DEMBehaviorManage();
            var actual = behaviorManage.CheckBinData(blist);
            //Assert
            Assert.Equal(expected, actual);
        }
        [Fact]
        public void CheckBinData_Length_ShouldFail()
        {
            //Arrange
            UInt32 expected = LibErrorCode.IDS_ERR_DEM_BIN_LENGTH_ERROR;
            var list = new byte[]{
                0x66, 0x42, 0x11,
                0x67, 0xAD, 0x95,
                0x68, 0xF1, 0xC8,
                0x69, 0x94, 0xC8,
                0x6A, 0xA1, 0x50,
                0x6B, 0x72, 0xD0,
                0x6C, 0x29, 0x9B,
                0x6D, 0x27, 0xBC,
                0x6E, 0x24, 0x45
            };
            List<byte> blist = new List<byte>(list);
            DEMBehaviorManage behaviorManage = new DEMBehaviorManage();
            var actual = behaviorManage.CheckBinData(blist);
            //Assert
            Assert.Equal(expected, actual);
        }
        [Fact]
        public void CheckBinData_Address_ShouldFail()
        {
            //Arrange
            UInt32 expected = LibErrorCode.IDS_ERR_DEM_BIN_ADDRESS_ERROR;
            var list = new byte[]{
                0x76, 0x42, 0x11,
                0x77, 0xAD, 0x95,
                0x78, 0xF1, 0xC8,
                0x79, 0x94, 0xC8,
                0x7A, 0xA1, 0x50,
                0x7B, 0x72, 0xD0,
                0x7C, 0x29, 0x9B,
                0x7D, 0x27, 0xBC,
                0x7E, 0x24, 0x45,
                0x7F, 0x81, 0xAE
            };
            List<byte> blist = new List<byte>(list);
            DEMBehaviorManage behaviorManage = new DEMBehaviorManage();
            var actual = behaviorManage.CheckBinData(blist);
            //Assert
            Assert.Equal(expected, actual);
        }
    }
}
