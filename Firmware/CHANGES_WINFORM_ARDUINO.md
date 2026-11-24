# Nhật ký thay đổi WinForms C# (`BTL_UDMT`) & Firmware Arduino DMA

Tài liệu đối chiếu chi tiết từng vị trí đã chỉnh sửa giữa `BTL_UDMT/BTL_UDMT/Form1.Designer.cs`, `BTL_UDMT/BTL_UDMT/Form1.cs` và `firmware_winform_arduino/rp2040_dso_dma/rp2040_dso_dma.ino`.  
Số dòng cột **"Dòng (bản gốc)"** tương ứng trực tiếp với số dòng trước khi sửa của các tệp nguồn.

---

## 1. `BTL_UDMT/BTL_UDMT/Form1.Designer.cs`

| Dòng (bản gốc) | Đổi từ | Đổi sang | Lí do đổi |
| --- | --- | --- | --- |
| `58-59` | Chỉ khai báo hết `btnSaveImage`, `btnExportCSV`. | Khai báo thêm `grpTestGen`, `lblGenFreq`, `lblGenDuty`, `txtGenFreq`, `txtGenDuty`, `btnSetGen`. | Bổ sung nhóm điều khiển phát xung vuông test (`GPIO0`) ngay trong panel phải mà không phá bố cục sẵn có. |
| `188-198` | `cboTimeDiv.Items.AddRange` gồm 7 mức (`50 us/div` đến `5 ms/div`), `SelectedIndex = 3`. | Thêm `1 us/div`, `2 us/div`, `5 us/div`, `10 us/div`, `20 us/div` vào đầu danh sách (tổng 12 mức), đổi `SelectedIndex = 8` (`500 us/div`). | Cho phép quan sát xung tần số cao đến `200 kHz` ở dải thời gian nhỏ (`1–20 us/div`) và vẫn giữ mặc định khởi động tại `500 us/div`. |
| `227-229` | `pnlControlRight.Controls.AddRange` chỉ chứa `grpVertical, grpHorizontal, grpTrigger, grpTools`. | Khởi tạo `grpTestGen` (2 ô `TextBox` nhập `Freq (Hz)` mặc định `1000`, `Duty (%)` mặc định `50` và nút `Phát xung vuông`) rồi thêm vào `pnlControlRight`. | Cho phép người dùng nhập trực tiếp tần số và duty xung test trên WinForms theo yêu cầu. |

---

## 2. `BTL_UDMT/BTL_UDMT/Form1.cs`

| Dòng (bản gốc) | Đổi từ | Đổi sang | Lí do đổi |
| --- | --- | --- | --- |
| `14-16` | `ADC_VREF = 3.3;`, `ADC_MID_VOLT = 1.65;`, chưa có hằng số độ lợi AFE thật. | `ADC_VREF = 3.301;`, `ADC_MID_VOLT = 1.650;`, thêm `AFE_AMP_GAIN = (1.0 + 1000.0 / 560.0) * (1.0 + 1000.0 / 560.0 + 1000.0 / 3000.0);` (`≈ 8.6888×`). | Khớp đúng điện áp tham chiếu đo thực tế và độ lợi hai tầng khuếch đại không đảo `U6B`–`U6C` trên sơ đồ `OSCILLOSCOPE - offical_version.kicad_sch`. |
| `32-33` | Kết thúc nhóm biến trigger tại `triggerIndex = NUM_SAMPLES / 2;`. | Thêm `private byte hwSwByte = 0x12;` và `private int lastHwSw3 = -1;`. | Lưu trạng thái byte công tắc phần cứng mới nhất và phát hiện thời điểm công tắc cơ khí `SW3` (`X1`/`X5`) thực sự thay đổi. |
| `123-124` | `BindUIEvents` chỉ gắn sự kiện đến `btnSaveImage`, `btnExportCSV`. | Gắn thêm `btnSetGen.Click += (s, e) => SendGenCommandToRP2040();`. | Gửi lệnh đổi tần số và duty xuống RP2040 ngay khi nhấn nút phát xung vuông. |
| `182-184` | `BtnConnect_Click` đặt `isConnected = true; singleCaptured = false; SendRateCommandToRP2040();`. | Đặt thêm `lastHwSw3 = -1;` và gọi `SendGenCommandToRP2040();` ngay sau `SendRateCommandToRP2040();`. | Ép đồng bộ lại công tắc `SW3` và đồng bộ tần số/duty nguồn phát mẫu mỗi lần kết nối mới cổng COM. |
| `247-262` | `SendRateCommandToRP2040` gửi 1 ký tự `'0'..'5'` cho 7 mức `50 us..5 ms`. | Ánh xạ đủ 12 chỉ số (`0..3 -> R8` `3 MSa/s`, `4 -> R7` `1.5 MSa/s`, `5..6 -> R0` `500 kSPS` ... `11 -> R5` `10 kSPS`), gửi dạng `R<code>\n`; thêm `SendGenCommandToRP2040()` gửi `G<freq>,<duty>\n` (kẹp `10..500000 Hz`, `1..99%`). | Hỗ trợ dải lấy mẫu cao `1.5M`/`3.0M` ở `1–20 us/div` và truyền tham số tần số/duty nguyên vẹn qua cùng cổng USB-CDC. |
| `285-324` | `ParseBinaryFrames` đọc khung `2052` byte (`3 + 2048 + 1`) chỉ có `[0xAA, 0x55, rateCode]`. | Đọc khung `2053` byte (`4 + 2048 + 1`) có thêm `byte swByte = rxBuffer[3]`, tính `calcChecksum = (byte)(rateCode ^ swByte)` rồi XOR với `2048` byte dữ liệu tại `rxBuffer[4 + i]`, cập nhật `hwSwByte = swByte;`. | Truyền kèm trạng thái 3 công tắc phần cứng `SW1/SW2/SW3` trong mỗi frame mà vẫn giữ tính toàn vẹn bằng checksum XOR. |
| `342-370` | `GetSamplingFrequencyHz` và `GetTimePerDivSeconds` chỉ định nghĩa 7 chỉ số (`50 us/div..5 ms/div`). | Mở rộng sang 12 chỉ số (`1 us/div..5 ms/div` ứng với `3 MHz`, `1.5 MHz`, `500 kHz..10 kHz`). | Đảm bảo tính chu kỳ `T`, tần số `Freq`, con trỏ đo `dT` và xuất CSV đúng ở các thang `1–20 us/div`. |
| `377-397` | `ProcessSignalAndMeasurements` dùng công thức DSO138 đảo dấu `inputSignalVolts[i] = (ADC_MID_VOLT - rawAdcVolts[i]) / (6.0 * alpha1 * alpha2)` với `alpha1 = 0.01`, `alpha2 = 0.1/0.4/1.0`. | Sửa công thức thuận dấu `(rawAdcVolts[i] - ADC_MID_VOLT) / totalGain`, sửa `1V` về `20.0 / 2020.0`, dùng `hwAlpha2` cố định theo phần cứng (`X1 = 1.0`, `X5 = 0.2`) và bổ sung `SyncSwitchesFromHardware()`. | Khắc phục triệt để lỗi lật ngược cực tính và sai hệ số nhân (`6.0` vs `8.6888`), tự động đồng bộ `DC/AC/GND`, `1V/100mV/10mV`, `X1/X5` từ bo mạch, đồng thời cho phép `X2` phóng/thu hiển thị phần mềm mà không làm sai điện áp thật. |
| `532-539` | `RenderTimer_Tick` gọi thẳng `ProcessSignalAndMeasurements()` rồi `picScope.Invalidate()`. | Chèn `SyncSwitchesFromHardware();` ngay trước `ProcessSignalAndMeasurements();`. | Tự động cập nhật các nút chọn trên giao diện WinForms theo trạng thái công tắc vật lý ở mỗi khung hình. |

---

## 3. `firmware_winform_arduino/rp2040_dso_dma/rp2040_dso_dma.ino`

| Dòng (bản gốc) | Đổi từ | Đổi sang | Lí do đổi |
| --- | --- | --- | --- |
| `14-48` | Chỉ khai báo `PIN_ADC_IN`, `PIN_TEST_PWM`, `PIN_TRIG_PWM`, `FRAME_HEADER_SIZE 3` (`2052` byte), `RATE_CLKDIV[6]`. | Bổ sung `<hardware/clocks.h>`, `<hardware/vreg.h>`, các chân `PIN_ADC_VOL 27`, `PIN_ADC_MODE 28`, `PIN_SW3 22`, đặt `FRAME_HEADER_SIZE 4` (`2053` byte), `RATE_CLKDIV[9]` và biến trạng thái generator/boost (`genFreqHz`, `genDutyPct`, `pllBoosted`, `adcBoosted`). | Bổ sung đọc 3 công tắc phần cứng trên mạch và mở rộng bộ chia cho `1.0 / 1.5 / 3.0 MSa/s` trên cùng file `.ino`. |
| `59-69` | `setupPwmOutputs` cố định `GPIO0` ở `1 kHz` và `applySampleRate` chỉ dùng `48 MHz PLL_USB` (`0..5`). | Thêm `applyGenPwm(freqHz, dutyPct)` tự tính `clkDiv` và `wrap` theo `clock_get_hz(clk_sys)`; thêm `ensureBoostPll()` tăng `VREG_VOLTAGE_1_20` và gọi `set_sys_clock_khz(288000, true)` khi chọn mã `6..8` (`96 / 144 / 288 MHz` cho `clk_adc`). | Đạt tốc độ lấy mẫu đến `3.00 MSa/s` sạch nhiễu (tránh vùng `>= 336 MHz` làm kẹt bit-11 MSB ADC) và chỉnh được xung vuông `10..500000 Hz`, duty `1..99%` bất kể `clk_sys` đang ở `125` hay `288 MHz`. |
| `71-105` | Chỉ khởi tạo `ADC0` và đọc lệnh đơn ký tự `'0'..'5'`. | Khởi tạo thêm `ADC1`, `ADC2`, `PIN_SW3`; thêm `read3LevelChannel()` (lấy trung vị 5 mẫu + xả tụ S&H) và `readSwitchByte()` (tạm hạ `clk_adc` về `48 MHz` mỗi `60 ms`); đổi `readHostCommand()` sang nhận dòng `R<code>` và `G<freq>,<duty>`. | Đọc chính xác công tắc 3 nấc mà không nhiễu khi đang chạy boost `clk_adc`, đồng thời nhận được cả lệnh tốc độ lẫn tham số xung test. |
| `127-174` | `buildAndSendFrame(rateCode)` đóng gói 3 byte header (`2052` byte) và `loop()` chỉ gọi `captureBlockDMA()`. | `buildAndSendFrame(rateCode, swByte)` đóng `swByte` vào `txFrame[3]` và gộp vào XOR checksum (`2053` byte); `loop()` gọi `readSwitchByte()` trước khi chụp DMA. | Đồng bộ trực tiếp trạng thái công tắc phần cứng về WinForms trên từng khung hình với chi phí CPU tối thiểu. |

---

## 4. Bản vá treo khi chuyển chế độ `Auto` / `Normal` / `Single` (`BTL_UDMT/BTL_UDMT/Form1.cs`)

| Dòng (trước bản vá) | Đổi từ | Đổi sang | Lí do đổi |
| --- | --- | --- | --- |
| `27-36` | Dùng trực tiếp `inputSignalVolts` trong `lock (dataLock)`, các cờ `isConnected`, `singleCaptured`, `hwSwByte` không có `volatile`, chưa có `hasNewFrame`, `hasNormalFrame`. | Thêm bộ đệm trung gian `workVolts`, đánh dấu `volatile` cho `isConnected`, `singleCaptured`, `hwSwByte`, thêm cờ `hasNewFrame` và `hasNormalFrame`. | Tách vùng đọc dữ liệu mới khỏi vùng hiển thị sóng đang giữ ở chế độ `Normal` / `Single` và tránh đọc giá trị cũ từ cache giữa luồng `SerialPort` với luồng UI. |
| `91-94` | Gắn 3 lambda `CheckedChanged` trực tiếp chạy trên cả nút bị bỏ chọn lẫn nút được chọn (`singleCaptured = false`). | Gắn chung `trigModeHandler` chỉ kích hoạt khi `rb.Checked == true` để gọi `ResetTriggerMode()`, bổ sung `radTrigSingle.Click` để tái kích hoạt `Single`. | Tránh reset hai lần liên tiếp khi đổi `RadioButton` và cho phép bấm lại `Single` để chụp phát mới mà không phải chuyển qua chế độ khác. |
| `136-144` | Khởi tạo `SerialPort` với `ReadBufferSize` mặc định `4096` byte và timeout ghi/đọc mặc định (`InfiniteTimeout`). | Đặt `ReadBufferSize = 65536`, `ReadTimeout = 200`, `WriteTimeout = 200`. | Ngăn tràn nhanh bộ đệm nhận USB-CDC và chặn đứng tình huống luồng giao diện bị treo vô hạn trong `serialPort.Write()` / `Close()`. |
| `180-245` | `BtnConnect_Click` chưa reset `hasNewFrame`/`hasNormalFrame`/`triggerFound`; `DisconnectSerial` và `Form1_FormClosing` đóng cổng `serialPort.Close()` trước khi hạ `isConnected = false`. | Reset đủ các cờ trạng thái khi kết nối/ngắt kết nối; hạ `isConnected = false` **trước** khi gọi `serialPort.Close()`. | Đảm bảo luồng nhận `SerialPort_DataReceived` thoát an toàn trước khi đóng handle cổng COM. |
| `288-349` | `SerialPort_DataReceived` `return` sớm khi `singleCaptured == true` mà không đọc cổng COM; `ParseBinaryFrames` dùng `rxBuffer.RemoveAt(0)` trong vòng lặp. | Bổ sung `ResetTriggerMode()`; trong `SerialPort_DataReceived` luôn vòng lặp đọc hết `BytesToRead` ngay cả khi `singleCaptured == true` (chỉ bỏ qua không giải mã); `ParseBinaryFrames` giới hạn `rxBuffer` tối đa `4 * FRAME_SIZE`, tìm đồng bộ `0xAA` bằng `IndexOf(0xAA, 1)` và bật `hasNewFrame = true`. | Khắc phục nguyên nhân gốc làm treo app: trước đây dừng đọc khi `Single` chụp xong khiến đầy bộ đệm USB-CDC làm RP2040 kẹt ở `Serial.write()`, nên khi chuyển sang `Auto`/`Normal` sự kiện `DataReceived` không bao giờ kích lại; đồng thời loại bỏ chi phí `O(N^2)` khi tái đồng bộ khung. |
| `437-521` | `ProcessSignalAndMeasurements` xử lý lại `rawAdcVolts` ở mỗi nhịp `33 ms` kể cả khi chưa có khung mới, và ghi đè `inputSignalVolts` ngay cả khi `Normal` chưa bắt được trigger. | Thoát sớm nếu `!hasNewFrame`; xử lý và tìm sườn trigger trên `workVolts` trước, chỉ sao chép sang `inputSignalVolts` và cập nhật thông số đo khi có trigger ở chế độ `Normal`/`Single` (đặt `hasNormalFrame = true`). | Giữ ổn định dạng sóng hợp lệ cuối cùng trên màn hình ở chế độ `Normal` thay vì nhấp nháy trắng khi tần số quét nhanh lọt giữa hai sườn xung, đồng thời không trigger lặp trên khung cũ. |
| `624-627` | Điều kiện vẽ sóng ở `PicScope_Paint` là `if (!radTrigNorm.Checked \|\| triggerFound \|\| singleCaptured)`. | Đổi thành `if ((!radTrigNorm.Checked && !radTrigSingle.Checked) \|\| hasNormalFrame \|\| singleCaptured)`. | Chỉ vẽ dạng sóng ở `Normal` khi đã có ít nhất một lần bắt được trigger (`hasNormalFrame`) và ở `Single` khi đã chốt mẫu (`singleCaptured`). |

---

## 5. Bản vá treo giao diện khi chọn sai cổng COM (`BTL_UDMT/BTL_UDMT/Form1.cs`)

| Dòng (trước bản vá) | Đổi từ | Đổi sang | Lí do đổi |
| --- | --- | --- | --- |
| `5-7` | Chỉ import đến `System.IO.Ports;` và `System.Windows.Forms;`. | Thêm `using System.Threading.Tasks;`. | Hỗ trợ thực thi `Open()`, `Write()`, `Close()` bất đồng bộ trên thread nền, không khóa luồng giao diện. |
| `22-35` | Dùng chung một `serialPort` tạo sẵn từ lúc mở Form; chưa có `writeLock`, `isConnecting`, `isClosing`, `gotValidFrame`, `connectSeq`. | Bổ sung `writeLock` và các cờ `isConnecting`, `isClosing`, `gotValidFrame`, `connectSeq`. | Quản lý hủy/chống chồng lấn phiên kết nối và bảo vệ ghi lệnh trên thread nền. |
| `66`, `143-158` | Gọi `InitializeSerial()` dùng lại mãi một đối tượng `SerialPort`. | Bỏ `InitializeSerial()`; thay bằng `CreateSerialPort()` tạo `SerialPort` mới mỗi lần kết nối và `ClosePortAsync()` đóng/hủy cổng trên `Task.Run`. | Tránh kẹt vĩnh viễn vào một handle cổng COM cũ đang treo trong driver Windows khi người dùng ngắt kết nối hoặc đóng cửa sổ `[X]`. |
| `176-221` | `BtnConnect_Click` chạy đồng bộ `serialPort.Open()` trên luồng UI. | Chuyển sang `async void`, mở cổng trong `Task.Run` với `Task.WhenAny(..., Task.Delay(2000))`; mở xong chờ tối đa `2000 ms` tới khi nhận được ít nhất 1 khung `0xAA 0x55` hợp lệ (`gotValidFrame`), nếu không sẽ tự ngắt và báo sai cổng COM. | Chọn nhầm cổng COM ảo/Bluetooth bị treo `CreateFileW` hoặc cổng của thiết bị khác sẽ không làm đơ UI hay đơ nút đóng cửa sổ. |
| `228-264` | `DisconnectSerial()` và `Form1_FormClosing()` gọi `serialPort.Close()` đồng bộ trên luồng UI. | Tách `SetDisconnectedUI()`; cả hai hàm cập nhật cờ/giao diện ngay lập tức rồi đóng cổng qua `ClosePortAsync(port)`. | Bấm `Disconnect` hoặc nút đóng `[X]` của cửa sổ luôn phản hồi tức thì ngay cả khi driver cổng COM đang treo. |
| `272-305` | `SendRateCommandToRP2040()` và `SendGenCommandToRP2040()` gọi `serialPort.Write()` trực tiếp trên luồng UI. | Gửi lệnh qua `WriteCommandAsync(cmd)` dùng `Task.Run` và `lock (writeLock)`. | Ngăn lệnh ghi `R<code>` / `G<freq>,<duty>` làm đứng giao diện nếu cổng COM vừa rút hoặc kẹt luồng TX. |
| `320-354`, `399-401` | `SerialPort_DataReceived` lặp `while` không giới hạn số lượt đọc; `ParseBinaryFrames` chưa đánh dấu `gotValidFrame`. | Kiểm tra đúng thực thể `port == serialPort`, giới hạn tối đa `8` lượt đọc x `8192` byte mỗi lần gọi callback; bật `gotValidFrame = true` khi qua checksum. | Chống nghẽn threadpool nếu chọn nhầm cổng xả rác liên tục và xác nhận đúng luồng dữ liệu RP2040. |
| `674`, `726` | `RenderTimer_Tick` và `DrawTopStatusBar` chỉ kiểm tra `isConnected`. | Kiểm tra thêm `gotValidFrame` (`isConnected && gotValidFrame`). | Chỉ chuyển trạng thái đo và vẽ sóng sau khi đã xác thực nhận khung hợp lệ từ RP2040. |

---

## 6. Hiển thị tốc độ lấy mẫu ở góc trên phải màn hình (`BTL_UDMT/BTL_UDMT/Form1.cs`)

| Dòng (trước cập nhật) | Đổi từ | Đổi sang | Lí do đổi |
| --- | --- | --- | --- |
| `44` | Chỉ lưu `hwSwByte`. | Thêm `private volatile byte hwRateCode = 2;`. | Lưu lại mã tốc độ lấy mẫu thực tế (`0..8`) mà RP2040 gửi kèm trong header của mỗi frame. |
| `497-500` | `ParseBinaryFrames` đọc `rateCode` để tính checksum rồi bỏ qua không lưu. | Gán thêm `hwRateCode = rateCode;` khi frame qua kiểm tra checksum. | Đồng bộ trực tiếp tốc độ lấy mẫu phần cứng đang chạy lên giao diện hiển thị. |
| `548` | Chỉ có `GetSamplingFrequencyHz()` suy từ `cboTimeDiv`. | Thêm `GetRateHzFromCode(byte code)` ánh xạ mã `0..8` sang `10 kSa/s .. 3.00 MSa/s`. | Quy đổi chính xác mã tốc độ từ frame phần cứng sang đơn vị Hz thực tế. |
| `840-842` | `DrawTopStatusBar` chỉ vẽ `[CPL]`, `Scale`, `Timebase`, `Trig` ở bên trái thanh trạng thái. | Tính `fs` từ `hwRateCode` khi đang kết nối hợp lệ (hoặc `GetSamplingFrequencyHz()` khi offline) và vẽ `Fs: ... MSa/s` / `kSa/s` căn sát góc trên phải (`width - fsSize.Width - 10, 7`). | Hiển thị trực quan tốc độ lấy mẫu hiện hành ngay góc trên phải màn hình oscilloscope. |
