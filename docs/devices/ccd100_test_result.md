# CCD100 TCP/IP Communication Test Result

**Test Date:** 2026-02-20
**Host:** 192.168.1.180
**Port:** 101
**Protocol:** TCP/IP

---

## Device Information

| Item | Value |
|------|-------|
| Display Value | 0.004 Torr |
| Setpoint Value | 0.000 |
| Display Unit | Torr |
| Serial Number | 241111 |

---

## Current Settings (from `aras` response)

```
SETTINGS:Torr ,10.000  ,10.000  ,0.000   ,100.0   , , ,0.000   ,100.0   , ,0.20,,1.000   , 2.0,10.000  , 2.0,241111,
```

| Parameter | Value |
|-----------|-------|
| Unit | Torr |
| Range Max | 10.000 |
| Range Min | 0.000 |
| Full Scale | 100.0 |
| Serial Number | 241111 |

---

## Test Log

### Test 1: Read Display Value (r)
```
TX: ar
RX: READ:0.004 ;0
!a!o!
```
**Status:** SUCCESS

### Test 2: Read All Settings (ras)
```
TX: aras
RX: SETTINGS:Torr ,10.000  ,10.000  ,0.000   ,100.0   , , ,0.000   ,100.0   , ,0.20,,1.000   , 2.0,10.000  , 2.0,241111,!a!o!
```
**Status:** SUCCESS

### Test 3: Read Input Type (it?)
```
TX: ait?
RX: *a*:it ?;
!a!b!
```
**Status:** FAILED (Command not recognized)

### Test 4: Read Decimal Point (dp?)
```
TX: adp?
RX: *a*:dp ?;
!a!b!
```
**Status:** FAILED (Command not recognized)

### Test 5: Read Display Units (du?)
```
TX: adu?
RX: *a*:du ?;
!a!b!
```
**Status:** FAILED (Command not recognized)

### Test 6: Read Setpoint Value (spv?)
```
TX: aspv?
RX: *a*:spv?;
SP VALUE: 0.000
!a!o!
```
**Status:** SUCCESS

### Test 7: Read Setpoint Hysteresis (sph?)
```
TX: asph?
RX: *a*:sph?;
!a!b!
```
**Status:** FAILED (Command not recognized)

### Test 8: Read IP Address (ip?)
```
TX: aip?
RX: *a*:ip ?;
!a!b!
```
**Status:** FAILED (Command not recognized)

### Test 9: Read Subnet Mask (sm?)
```
TX: asm?
RX: *a*:sm ?;
!a!b!
```
**Status:** FAILED (Command not recognized)

### Test 10: Read Gateway (gw?)
```
TX: agw?
RX: *a*:gw ?;
!a!b!
```
**Status:** FAILED (Command not recognized)

---

## Response Code Reference

| Code | Meaning |
|------|---------|
| `!a!o!` | OK - Command executed successfully |
| `!a!b!` | Bad - Command not recognized or invalid syntax |

---

## Result

**Status:** PARTIAL SUCCESS

- TCP/IP connection established successfully
- Core commands (`ar`, `aras`, `aspv?`) working
- Some commands may require different syntax or are not available via TCP/IP

---

## Notes

- The device is responding on port 101
- Current pressure reading: 0.004 Torr (near vacuum)
- Device serial number: 241111
