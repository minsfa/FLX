"""
CCD100 Configurable Process Display Controller - TCP/IP Communication Test
Port 101 TCP/IP Communication Test Script
"""

import socket
import time

# Communication settings
HOST = '192.168.1.180'  # Default IP address
PORT = 101              # TCP port
TIMEOUT = 5

def send_command(sock, command):
    """Send command and receive response"""
    # Add protocol format: a<command><CR><LF>
    full_cmd = f"a{command}\r\n"
    print(f"TX: a{command}")

    sock.sendall(full_cmd.encode('ascii'))
    time.sleep(0.2)

    response = sock.recv(1024).decode('ascii', errors='ignore')
    response_clean = response.strip()
    print(f"RX: {response_clean}")
    return response_clean

def main():
    print("=" * 50)
    print("CCD100 TCP/IP Communication Test")
    print("=" * 50)
    print(f"Host: {HOST}")
    print(f"Port: {PORT}")
    print("=" * 50)

    try:
        # Create TCP socket
        sock = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
        sock.settimeout(TIMEOUT)

        # Connect to device
        print(f"Connecting to {HOST}:{PORT}...")
        sock.connect((HOST, PORT))
        print(f"[OK] Connected successfully\n")

        # Test 1: Read current display value
        print("[Test 1] Read Display Value (r)")
        send_command(sock, "r")
        print()

        # Test 2: Read all settings
        print("[Test 2] Read All Settings (ras)")
        send_command(sock, "ras")
        print()

        # Test 3: Read input type
        print("[Test 3] Read Input Type (it?)")
        send_command(sock, "it?")
        print()

        # Test 4: Read decimal point position
        print("[Test 4] Read Decimal Point (dp?)")
        send_command(sock, "dp?")
        print()

        # Test 5: Read display units
        print("[Test 5] Read Display Units (du?)")
        send_command(sock, "du?")
        print()

        # Test 6: Read setpoint value
        print("[Test 6] Read Setpoint Value (spv?)")
        send_command(sock, "spv?")
        print()

        # Test 7: Read setpoint hysteresis
        print("[Test 7] Read Setpoint Hysteresis (sph?)")
        send_command(sock, "sph?")
        print()

        # Test 8: Read IP address setting
        print("[Test 8] Read IP Address (ip?)")
        send_command(sock, "ip?")
        print()

        # Test 9: Read subnet mask
        print("[Test 9] Read Subnet Mask (sm?)")
        send_command(sock, "sm?")
        print()

        # Test 10: Read gateway
        print("[Test 10] Read Gateway (gw?)")
        send_command(sock, "gw?")
        print()

        # Close socket
        sock.close()
        print("[OK] Connection closed")
        print("\n" + "=" * 50)
        print("Communication test completed!")
        print("=" * 50)

    except socket.timeout:
        print(f"[ERROR] Connection timeout - device not responding")
    except ConnectionRefusedError:
        print(f"[ERROR] Connection refused - check if device is online")
    except OSError as e:
        print(f"[ERROR] Network error: {e}")
    except Exception as e:
        print(f"[ERROR] {e}")

if __name__ == "__main__":
    main()
