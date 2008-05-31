using System;
using System.Globalization;
using System.IO;
using System.IO.Ports;
using log4net;
using Modbus.IO;
using Modbus.Message;
using Unme.Common;

namespace Modbus.Device
{
	/// <summary>
	/// Modbus serial slave device.
	/// </summary>
	public class ModbusSerialSlave : ModbusSlave
	{
		private static readonly ILog _log = LogManager.GetLogger(typeof(ModbusSerialSlave));

		private ModbusSerialSlave(byte unitId, ModbusTransport transport)
			: base(unitId, transport)
		{
		}

		/// <summary>
		/// Modbus ASCII slave factory method.
		/// </summary>
		public static ModbusSerialSlave CreateAscii(byte unitId, SerialPort serialPort)
		{
			return new ModbusSerialSlave(unitId, new ModbusAsciiTransport(new CommPortAdapter(serialPort)));
		}

		/// <summary>
		/// Modbus RTU slave factory method.
		/// </summary>
		public static ModbusSerialSlave CreateRtu(byte unitId, SerialPort serialPort)
		{
			return new ModbusSerialSlave(unitId, new ModbusRtuTransport(new CommPortAdapter(serialPort)));
		}

		/// <summary>
		/// Start slave listening for requests.
		/// </summary>
		public override void Listen()
		{
			// TODO consider implementing bridge pattern for Devce <-> Transport mappings
			ModbusSerialTransport serialTransport = (ModbusSerialTransport) Transport;					

			while (true)
			{
				try
				{
					try
					{
						// read request and build message
						byte[] frame = Transport.ReadRequest();
						IModbusMessage request = ModbusMessageFactory.CreateModbusRequest(frame);

						if (serialTransport.CheckFrame && !serialTransport.ChecksumsMatch(request, frame))
						{
							string errorMessage = String.Format(CultureInfo.InvariantCulture, "Checksums failed to match {0} != {1}", request.MessageFrame.Join(", "), frame.Join(", "));
							_log.Error(errorMessage);
							throw new IOException(errorMessage);
						}

						// only service requests addressed to this particular slave
						if (request.SlaveAddress != UnitID)
						{
							_log.DebugFormat("NModbus Slave {0} ignoring request intended for NModbus Slave {1}", UnitID, request.SlaveAddress);
							continue;
						}

						// perform action
						IModbusMessage response = ApplyRequest(request);

						// write response
						Transport.Write(response);
					}
					catch (IOException ioe)
					{
						_log.ErrorFormat("IO Exception encountered while listening for requests - {0}", ioe.Message);
						serialTransport.DiscardInBuffer();
					}
					catch (TimeoutException te)
					{
						_log.ErrorFormat("Timeout Exception encountered while listening for requests - {0}", te.Message);
						serialTransport.DiscardInBuffer();
					}
				}
				catch (InvalidOperationException)
				{
					// when the underlying port is closed
					break;
				}
			}
		}
	}
}
