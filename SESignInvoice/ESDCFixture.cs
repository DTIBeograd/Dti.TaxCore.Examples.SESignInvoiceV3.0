using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PCSC;
using PCSC.Iso7816;

namespace Dti.TaxCore.Examples.SESignInvoice
{
    [TestClass]
    public class ESDCFixture
    {
        [TestMethod]
        public void SignInvoice()
        {
            #region Data used to create Invoice Request

            //ISO8601 DateTime "2017-07-25T10:17:18.953Z" converted to miliseconds
            var dateTime = ConvertUInt64To8ByteBigEndianArray(1500977838953);

            //TIN "021131682"
            var taxpayerId = new byte[20] { 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x30, 0x32, 0x31, 0x31, 0x33, 0x31, 0x36, 0x38, 0x32 };

            //Buyer TIN ""
            var buyerId = new byte[20] { 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 };

            //Invoice Type "Normal"
            var invoiceType = new byte[1] { 0x00 };

            //Transaction Type "Sale"
            var transactionType = new byte[1] { 0x00 };

            //Total Invoice Amount "240"
            var invoiceAmount = ConvertAmountToUInt64_7ByteBigEndianArray(240);

            //Number of tax categories used in invoice request "1"
            var categoryCountOninvoice = new byte[1] { 0x01 };

            //Tax Category position e.g. VAT "3"
            var taxCategory = new byte[1] { 0x03 };

            //Amount of tax category VAT "13.5849"
            var taxCategoryAmount = ConvertAmountToUInt64_7ByteBigEndianArray(13.5849m);

            #endregion

            #region Prepare Sign Invoice request

            var payload = new byte[66];

            dateTime.CopyTo(payload, 0);

            taxpayerId.CopyTo(payload, 8);

            buyerId.CopyTo(payload, 28);

            invoiceType.CopyTo(payload, 48);

            transactionType.CopyTo(payload, 49);

            invoiceAmount.CopyTo(payload, 50);

            categoryCountOninvoice.CopyTo(payload, 57);

            taxCategory.CopyTo(payload, 58);

            taxCategoryAmount.CopyTo(payload, 59);

            #endregion

            #region Send Pin Verify Command

            var iSSecureElement = false;

            IsoReader reader = GetReader(ref iSSecureElement);

            var responseSCard = reader.Transmit(PINVerify(reader));

            Assert.AreEqual(responseSCard.SW1, (byte)SW1Code.Normal);

            #endregion Send Pin Verify Command

            #region Send Sign Invoice command

            responseSCard = reader.Transmit(SignInvoiceCommand(reader, payload));

            Assert.AreEqual(responseSCard.SW1, (byte)SW1Code.Normal);

            #endregion
        }

        #region Reader Initialization

        private IsoReader GetReader(ref bool iSSecureElement)
        {
            IsoReader reader;
            // Establish SCard context
            SCardContext hContext = new SCardContext();
            hContext.Establish(SCardScope.System);
            // Retrieve the list of Smartcard readers
            string[] szReaders = hContext.GetReaders();

            // Create a reader object using the existing context
            reader = new IsoReader(hContext);

            // Connect to the card
            foreach (var sZReader in szReaders)
            {
                reader.Connect(sZReader,
                SCardShareMode.Shared,
                SCardProtocol.T0 | SCardProtocol.T1);

                var response = reader.Transmit(GetSelectAppBytes(reader));
                if (response.SW1 == 0x90)
                {
                    iSSecureElement = true;
                    break;
                }

            }
            return reader;
        }

        #endregion

        #region APDU commands

        private CommandApdu GetSelectAppBytes(IsoReader isoReader)
        {
            return new CommandApdu(IsoCase.Case4Short, isoReader.ActiveProtocol)
            {
                CLA = 0x00,
                Instruction = InstructionCode.SelectFile,
                P1 = 0x04,
                P2 = 0x00,
                Data = new byte[] { 0xA0, 0x00, 0x00, 0x07, 0x48, 0x46, 0x4A, 0x49, 0x2D, 0x54, 0x61, 0x78, 0x43, 0x6F, 0x72, 0x65 }
            };
        }

        public CommandApdu PINVerify(IsoReader reader)
        {
            return new CommandApdu(IsoCase.Case3Short, reader.ActiveProtocol)
            {
                CLA = 0x88,
                INS = 0x11,
                P1 = 0x04,
                P2 = 0x00,
                Data = GetIntArray(Properties.Settings.Default.PIN)
            };
        }

        public CommandApdu SignInvoiceCommand(IsoReader reader, byte[] data)
        {
            return new CommandApdu(IsoCase.Case4Extended, reader.ActiveProtocol)
            {
                CLA = 0x88,
                INS = 0x13,
                P1 = 0x04,
                P2 = 0x00,
                Data = data
            };
        }

        #endregion

        #region Support

        private byte[] ConvertUInt64To8ByteBigEndianArray(UInt64 counter)
        {
            return BitConverter.GetBytes(counter).Reverse().ToArray();
        }

        private byte[] ConvertAmountToUInt64_7ByteBigEndianArray(decimal amount)
        {
            var bigEndian = BitConverter.GetBytes((ulong)(HalfRoundUp4(amount) * 10000)).Reverse();
            bigEndian = bigEndian.Skip(1).Take(7);
            return bigEndian.ToArray();
        }

        private decimal HalfRoundUp4(decimal value)
        {
            return Math.Round(value, 4, MidpointRounding.AwayFromZero);
        }

        private byte[] GetIntArray(int num)
        {
            List<byte> listOfInts = new List<byte>();
            while (num > 0)
            {
                listOfInts.Add((byte)(num % 10));
                num = num / 10;
            }
            listOfInts.Reverse();
            return listOfInts.ToArray();
        }

        #endregion
    }
}
