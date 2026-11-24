# Firmware C/Arduino DMA cho WinForms `BTL_UDMT`

Thư mục này chứa firmware **C/Arduino (DMA)** viết riêng cho ứng dụng WinForms C# trong `BTL_UDMT/`, độc lập hoàn toàn với bộ mã nguồn Python trong `software/` và file mạch KiCad trong `OSCILLOSCOPE/`.

## Cấu trúc thư mục

```text
firmware_winform_arduino/
├── BTL_UDMT_WinForms/
│   └── Các file winform
├── RP_2040/
│   └── rp2040_dso_dma.ino        # Sketch Arduino IDE dùng Pico SDK ADC + DMA (hỗ trợ boost 3 MSa/s)
├── CHANGES_WINFORM_ARDUINO.md    # Chi tiết từng dòng đã sửa giữa BTL_UDMT WinForms và Arduino .ino
└── README.md
```

## Hướng dẫn nạp bằng Arduino IDE

1. Mở **Arduino IDE** → `File -> Preferences`, thêm URL Board Manager của **Earle Philhower `arduino-pico`**:
   `https://github.com/earlephilhower/arduino-pico/releases/download/global/package_rp2040_index.json`
2. Vào `Tools -> Board -> Boards Manager`, cài đặt **Raspberry Pi Pico/RP2040**.
3. Chọn board **Raspberry Pi Pico**, chọn đúng cổng **COM** (nếu bo đang chạy MicroPython thì giữ nút **BOOT** + bấm **RST** để hiện ổ `RPI-RP2` trước).
4. Mở `firmware_winform_arduino/rp2040_dso_dma/rp2040_dso_dma.ino` và bấm **Upload**.
5. Chạy `BTL_UDMT/BTL_UDMT/bin/Debug/BTL_UDMT.exe`, chọn cổng COM và bấm **Connect**.

## Sơ đồ chân RP2040 (`OSCILLOSCOPE - offical_version.kicad_sch`)

| Chân | Chức năng | Ghi chú |
| --- | --- | --- |
| `GPIO26` (`ADC0`) | `/ADC_in` | Thu tín hiệu analog 12-bit qua DMA (`DREQ_ADC`) |
| `GPIO27` (`ADC1`) | `/Vol_sel` | Đọc công tắc `SW2`: `0 = 1V`, `1 = 100mV`, `2 = 10mV` |
| `GPIO28` (`ADC2`) | `/Mode_sel` | Đọc công tắc `SW1`: `0 = GND`, `1 = AC`, `2 = DC` |
| `GPIO22` (`IO`) | `/Scope_sel` | Đọc công tắc `SW3`: `1 = X1`, `0 = X5` (`X2` chỉnh trên phần mềm) |
| `GPIO0` (`PWM`) | Xung test vuông | Chỉnh được tần số (`10..500000 Hz`) & Duty (`1..99%`) từ WinForms |
| `GPIO20` (`PWM`) | `/Trig_PWM` | Xuất áp chuẩn `~1.65 V` (`100 kHz`, `50%` duty) |

## Giao thức nhị phân khớp `BTL_UDMT/BTL_UDMT/Form1.cs`

- **WinForms → RP2040 (lệnh ASCII kết thúc bằng `\n`):**
  - `R<code>\n`: chọn tốc độ lấy mẫu (`0=500k`, `1=250k`, `2=100k`, `3=50k`, `4=20k`, `5=10k` dùng `PLL_USB 48 MHz`; `6=1.0M`, `7=1.5M`, `8=3.0M` boost `PLL_SYS 288 MHz @ 1.20 V`).
  - `G<freq>,<duty>\n`: đổi tần số (`10..500000 Hz`) và độ rộng xung (`1..99 %`) của xung vuông test trên `GPIO0`.
- **RP2040 → WinForms (khung cố định `2053` byte, ~30 FPS):**
  `[0xAA][0x55][RateCode][SwByte][1024 mẫu × uint16 little-endian 12-bit (2048 byte)][Checksum XOR 1 byte]`

## Kiểm tra graph memory

```powershell
python firmware_winform_arduino/memory/refresh.py --check
python firmware_winform_arduino/memory/refresh.py --checkpoint "Mô tả thay đổi"
```
