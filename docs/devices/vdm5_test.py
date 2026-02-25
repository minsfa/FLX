"""
VDM-5 DiCAP Vacuum Pressure Transducer Communication Test
COM5 Serial Communication Test Script
"""

import serial
import time

# Communication settings
PORT = 'COM5'
BAUDRATE = 9600
TIMEOUT = 2
DEVICE_ADDRESS = 254  # Global address (always responds)

def send_command(ser, command):
    """Send command and receive response"""
    # Add protocol format: @<address><command>\
    full_cmd = f"@{DEVICE_ADDRESS}{command}\\"
    print(f"TX: {full_cmd}")

    ser.write(full_cmd.encode('ascii'))
    time.sleep(0.1)

    response = ser.read(1024).decode('ascii', errors='ignore')
    print(f"RX: {response}")
    return response

def main():
    print("=" * 50)
    print("VDM-5 DiCAP Communication Test")
    print("=" * 50)
    print(f"Port: {PORT}")
    print(f"Baudrate: {BAUDRATE}")
    print(f"Device Address: {DEVICE_ADDRESS}")
    print("=" * 50)

    try:
        # Open serial port
        ser = serial.Serial(
            port=PORT,
            baudrate=BAUDRATE,
            bytesize=serial.EIGHTBITS,
            parity=serial.PARITY_NONE,
            stopbits=serial.STOPBITS_ONE,
            timeout=TIMEOUT
        )
        print(f"[OK] Serial port {PORT} opened successfully\n")

        # Test 1: Read Serial Number
        print("[Test 1] Read Serial Number (SN)")
        send_command(ser, "SN?")
        print()

        # Test 2: Read Part Number
        print("[Test 2] Read Part Number (PN)")
        send_command(ser, "PN?")
        print()

        # Test 3: Read Firmware Version
        print("[Test 3] Read Firmware Version (FV)")
        send_command(ser, "FV?")
        print()

        # Test 4: Read Manufacturer
        print("[Test 4] Read Manufacturer (MF)")
        send_command(ser, "MF?")
        print()

        # Test 5: Read Pressure
        print("[Test 5] Read Pressure (P)")
        send_command(ser, "P?")
        print()

        # Test 6: Read Temperature
        print("[Test 6] Read Temperature (T)")
        send_command(ser, "T?")
        print()

        # Test 7: Read Current Unit
        print("[Test 7] Read Pressure Unit (U)")
        send_command(ser, "U?")
        print()

        # Test 8: Read Device Address
        print("[Test 8] Read Device Address (ADR)")
        send_command(ser, "ADR?")
        print()

        # Test 9: Read Baud Rate
        print("[Test 9] Read Baud Rate (BAUD)")
        send_command(ser, "BAUD?")
        print()

        # Test 10: Quick Query (combined data)
        print("[Test 10] Quick Query (Q)")
        send_command(ser, "Q?")
        print()

        # Close serial port
        ser.close()
        print("[OK] Serial port closed")
        print("\n" + "=" * 50)
        print("Communication test completed!")
        print("=" * 50)

    except serial.SerialException as e:
        print(f"[ERROR] Serial port error: {e}")
    except Exception as e:
        print(f"[ERROR] {e}")

if __name__ == "__main__":
    main()
