# VDM-5 DiCAP Communication Test Result

**Test Date:** 2026-02-20
**Port:** COM5
**Baudrate:** 9600
**Device Address:** 254 (Global)

---

## Device Information

| Item | Value |
|------|-------|
| Serial Number | 25211122348 |
| Part Number | VDM-5-40112102 |
| Firmware Version | 3.11 |
| Manufacturer | SENS4 |
| Device Address | 253 |
| Baud Rate | 9600 |

---

## Current Measurements

| Item | Value |
|------|-------|
| Pressure | 760.95 Torr |
| Temperature | 20.67 °C |
| Pressure Unit | TORR |

---

## Test Log

### Test 1: Read Serial Number (SN)
```
TX: @254SN?\
RX: @253ACK25211122348\
```

### Test 2: Read Part Number (PN)
```
TX: @254PN?\
RX: @253ACKVDM-5-40112102\
```

### Test 3: Read Firmware Version (FV)
```
TX: @254FV?\
RX: @253ACK3.11\
```

### Test 4: Read Manufacturer (MF)
```
TX: @254MF?\
RX: @253ACKSENS4\
```

### Test 5: Read Pressure (P)
```
TX: @254P?\
RX: @253ACK7.6095E+02\
```

### Test 6: Read Temperature (T)
```
TX: @254T?\
RX: @253ACK20.67\
```

### Test 7: Read Pressure Unit (U)
```
TX: @254U?\
RX: @253ACKTORR\
```

### Test 8: Read Device Address (ADR)
```
TX: @254ADR?\
RX: @253ACK253\
```

### Test 9: Read Baud Rate (BAUD)
```
TX: @254BAUD?\
RX: @253ACK9600\
```

### Test 10: Quick Query (Q)
```
TX: @254Q?\
RX: @253ACK7.6095E+2,----PZ,20.67,XXX,0\
```

---

## Result

**Status:** SUCCESS
All communication tests passed successfully.
