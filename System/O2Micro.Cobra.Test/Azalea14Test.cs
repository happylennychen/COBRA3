using O2Micro.Cobra.Azalea14;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace O2Micro.Cobra.Test
{
    public class Azalea14Test
    {
        [Theory]
        [InlineData(0x00, 0)]
        [InlineData(0xff,7968.75)]
        public void HexToRShouldWork(ushort input, double expected)
        {
            //Arrange
            //double expected = 7968.75;
            //Actual
            var actual = Formula.HexToR(input, 1, 0.625);
            //Assert
            Assert.Equal(expected, actual);
        }


        [Theory]
        [InlineData(0, 0x00)]
        [InlineData(7968.75, 0xff)]
        public void RToHexShouldWork(double input, ushort expected)
        {
            //Arrange

            //Actual
            var actual = Formula.RToHex(input, 1, 0.625);
            //Assert
            Assert.Equal(expected, actual);
        }
    }
}
