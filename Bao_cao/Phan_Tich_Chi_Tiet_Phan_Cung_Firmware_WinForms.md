# TÀI LIỆU PHÂN TÍCH CHI TIẾT PHẦN CỨNG, TÍNH TOÁN TRỌNG SỐ, GIẢI NGHĨA FIRMWARE DMA (`.ino`) VÀ KIẾN TRÚC WINFORMS (`BTL_UDMT`)

- **Dự án**: Máy hiện sóng số 1 kênh dựa trên vi điều khiển `YD-RP2040` và mạch tiền xử lý tương tự `TL084`
- **Sơ đồ nguyên lý chuẩn**: `OSCILLOSCOPE/OSCILLOSCOPE/OSCILLOSCOPE - offical_version.kicad_sch` (`102` linh kiện, `85` đường mạng kết nối, bo mạch 2 lớp `95 × 95 mm`)
- **Firmware nhúng**: `firmware_winform_arduino/rp2040_dso_dma/rp2040_dso_dma.ino` (`271` dòng, C/C++ Arduino-Pico SDK)
- **Phần mềm máy tính**: `BTL_UDMT/BTL_UDMT/Form1.cs` (`1071` dòng) & `Form1.Designer.cs` (`372` dòng)

---

## PHẦN 1: PHÂN TÍCH CHI TIẾT PHẦN CỨNG & TÍNH TOÁN SỐ LIỆU / TRỌNG SỐ

### 1.1. Tổng quan chuỗi truyền đạt tín hiệu và phân bố linh kiện

Toàn bộ hệ thống phần cứng trên sơ đồ chính thức gồm **102 linh kiện** chia thành 6 khối chức năng chính:
1. **Khối bảo vệ đầu vào và hạ áp chuyển mạch lệch áp (`12V DC → +8V / −6V`)**: `J2`, `J3`, `D1 (SS34)`, `D4 (P6KE12CA)`, cụm hạ áp dương `U1 (MP1584 → +8V)` và cụm đảo áp âm `U3 (MP1584 → −6V)`.
2. **Khối ổn áp tuyến tính và lọc nhiễu mắt cáo (`+8V / −6V → ±5V, +3.3V`)**: `U5 (KA78M05)`, mạch ổn áp âm tuyến tính rời `Q1 (PNP) + D5 (Zener 5.6V) + R43 (1k)`, `U2 (LM1117MP-3.3)`, cuộn cảm lọc `L6, L7 (100uH)` và hệ thống tụ lọc nguồn.
3. **Khối chọn chế độ ghép nối (`SW1`) và cầu phân áp bù tần số 3 thang đo (`SW2`)**: `J1 (BNC)`, `SW1 (DP3T)`, `C1 (100nF)`, `SW2 (DP3T)`, mạng điện trở chính xác 1% (`R17, R18, R19, R20, R21`) và mạng tụ bù tần số cố định (`C24, C25, C26, C27, C28, C29`).
4. **Khối đệm trở kháng cao (`U6A`), chia tinh (`SW3`) và khuếch đại hai tầng kèm dịch mức (`U6B`, `U6C`)**: Vi mạch `U6 (TL084 SOIC-14)`, chuỗi điện trở chia tinh `R26 (300Ω) – R27 (180Ω) – R28 (120Ω)`, cầu hồi tiếp tầng 1 (`R41 = 1k 1%`, `R42 = 560Ω 1%`) và cầu hồi tiếp cộng dịch mức tầng 2 (`R34 = 1k 1%`, `R33 = 560Ω 1%`, `R35 = 3k` nối `-5V`).
5. **Khối lọc thông thấp, kẹp áp bảo vệ ADC và so sánh trễ Schmitt Trigger (`U6D`)**: Mắt lọc `R36 (120Ω) + C30 (120pF)`, cặp diode Schottky kép `D8, D9 (BAT54S)`, bộ so sánh `U6D` (`R37 = 120Ω, R38 = 100kΩ`), mạch lọc ngưỡng `/Trig_PWM` (`R39 = 100kΩ, C31 = 100nF`) và mạch dịch mức ngõ ra `/Trigger` (`R40 = 1k, D7 = 1N4148W, R53 = 1k`).
6. **Khối vi điều khiển trung tâm (`U7: YD-RP2040`) và mạng phân áp đọc trạng thái công tắc**: 3 mạng phân áp đọc vị trí cơ khí của `SW1B` (`R16, R14, R15, R13 → /Mode_sel`), `SW2B` (`R22, R23, R24, R25 → /Vol_sel`), `SW3B` (`R29, R30, R31, R32 → /Scope_sel`).

---

### 1.2. Tính toán chi tiết Khối nguồn đối xứng (`12V → +8V / −6V → ±5V → +3.3V`)

#### a) Bảo vệ đầu vào nguồn `12V DC`
- **Diode chống ngược cực `D1 (SS34)`**: Diode Schottky dòng định mức $`3\,\text{A}`$, điện áp ngược cực đại $`40\,\text{V}`$, sụt áp thuận thấp $`V_{F(D1)} \approx 0.35\text{–}0.45\,\text{V}`$. Khi cấp nguồn adapter $`V_{\text{adapter}} = 12.0\,\text{V}`$, điện áp trên đường `Vin` sau `D1` là:

  ```math
  V_{\text{in}} = V_{\text{adapter}} - V_{F(D1)} \approx 12.0 - 0.4 = 11.6\,\text{V}
  ```

- **Diode dập xung quá áp `D4 (P6KE12CA)`**: TVS hai chiều công suất đỉnh $`600\,\text{W}`$, điện áp làm việc danh định $`V_{WM} = 10.2\,\text{V}`$, điện áp đánh thủng $`V_{BR} \in [11.4\,\text{V}; 12.6\,\text{V}]`$, điện áp ghim cực đại $`V_C \approx 16.7\,\text{V}`$. Khi cắm nóng dây nguồn dài có điện cảm ký sinh $`L_{\text{wire}}`$, xung quá áp cộng hưởng $`LC`$ bị `D4` ghim chặt dưới $`16.7\,\text{V}`$, bảo vệ an toàn tuyệt đối cho ngưỡng chịu áp $`28\,\text{V}`$ của `MP1584`. Việc cắt ngắn chân `D4` sát mặt đồng giúp giảm điện cảm chân dẫn $`L_{\text{lead}} \to 0`$, triệt tiêu điện áp rơi cảm ứng $`\Delta V = L_{\text{lead}} \cdot \frac{di}{dt}`$.

#### b) Hai tầng hạ áp chuyển mạch lệch áp `U1 (+8V)` và `U3 (−6V)` (`MP1584EN`)
- **Công thức điện áp hồi tiếp của `MP1584`**: Điện áp tham chiếu nội tại chân `FB` (Pin 4) là $`V_{\text{FB}} = 0.80\,\text{V}`$.
- **Nhánh dương `U1 (+8V)`**: Cầu phân áp hồi tiếp gồm biến trở tinh chỉnh `R5 (12k)` nối từ ngõ ra `+8V` về chân `FB` và điện trở `R6 = 8.2k` nối từ `FB` xuống `GNDREF`. Để đạt $`V_{+8\text{V}} = +8.00\,\text{V}`$, giá trị điện trở hồi tiếp tương đương cần thiết là:

  ```math
  V_{+8\text{V}} = V_{\text{FB}} \cdot \left(1 + \frac{R_5}{R_6}\right) \implies R_{5,\text{set}} = R_6 \cdot \left(\frac{8.00}{0.80} - 1\right) = 8.2\,\text{k}\Omega \times 9.0 = 73.8\,\text{k}\Omega
  ```

  (Hoặc sử dụng cầu phân áp đã hiệu chỉnh sẵn $`+8.0\,\text{V}`$ trên module buck rời.)

  > **Lưu ý:** Trên bo mạch thực nghiệm, hai cụm `U1` và `U3` được thay thế bằng 2 module buck rời chỉnh cố định đầu ra $`+8.0\,\text{V}`$ và $`-6.0\,\text{V}`$, kết hợp 1 diode phân cách nối tiếp ở ngõ vào module áp âm.

- **Ứng suất điện áp trên nhánh đảo áp âm `U3 (−6V)`**: Trong cấu hình Inverting Buck-Boost (chân `GND` của `U3` nối với đầu ra `-6V`), điện áp tổng đặt lên khóa MOSFET phía cao bên trong `U3` là:

  ```math
  V_{\text{stress}(U3)} = V_{\text{in}} + \lvert V_{-6\text{V}} \rvert \approx 11.6\,\text{V} + 6.0\,\text{V} = 17.6\,\text{V} \lt 28\,\text{V}
  ```

- **Lý do kỹ thuật chọn mức điện áp lệch (`+8V` / `−6V`) thay vì đối xứng (`±8V` hoặc `±6V`)**:
  - **Nhánh dương cần mức `+8.0V`**: Tầng ổn áp tuyến tính dương `U5 (KA78M05)` có điện áp rơi tối thiểu (dropout voltage) khá cao ($`V_{\text{drop}} \approx 2.0\,\text{V}`$), đồng thời nhánh `+5V` không chỉ nuôi `TL084` mà còn phải **gánh thêm toàn bộ tải của khối nguồn số `+3.3V`** (`U2: LM1117MP-3.3` cấp nguồn cho `YD-RP2040`). Đặt đầu ra `U1` ở $`+8.0\,\text{V}`$ tạo độ dự trữ áp $`\Delta V = 8.0 - 5.0 = 3.0\,\text{V} \gt V_{\text{drop(78M05)}} \approx 2.0\,\text{V}`$, giúp `U5` luôn ổn áp vững trong vùng tuyến tính ngay cả khi dòng tải số biến thiên.
  - **Nhánh âm chỉ cần mức `−6.0V`**: Mạch ổn áp âm tuyến tính rời `Q1 (PNP) + D5 (Zener 5.6V)` có điện áp rơi tối thiểu chỉ khoảng $`V_{\text{drop}} \approx 0.6\,\text{V}`$ và chỉ nuôi riêng nhánh âm dòng nhỏ của `TL084` ($`I_{CC} \approx 5.6\,\text{mA}`$). Do đó chỉ cần hạ xuống $`-6.0\,\text{V}`$ đã đủ biên ổn áp về $`-5.00\,\text{V}`$, đồng thời mang lại hai lợi ích trực tiếp cho tầng đảo áp `U3`: (1) **giảm gánh nặng điện áp tổng IC phải chịu** ($`17.6\,\text{V}`$ thay vì $`19.6\,\text{V}`$ nếu đặt $`-8.0\,\text{V}`$, giảm $`2.0\,\text{V}`$ ứng suất so với ngưỡng $`28\,\text{V}`$ của `MP1584`), và (2) **tăng nhẹ hiệu suất của mạch chuyển đổi** do tỷ số đảo áp và hệ số lấp đầy nhỏ hơn ($`D \approx \frac{6.0}{11.6 + 6.0} \approx 34.1\%`$ so với $`\approx 40.8\%`$ ở $`-8.0\,\text{V}`$), giảm tổn hao chuyển mạch/dẫn và giảm nhiệt tỏa ra trên tầng ổn áp âm.

#### c) Tầng ổn áp tuyến tính dương `+5V` (`U5: KA78M05`) và số `+3.3V` (`U2: LM1117MP-3.3`)
- **`U5 (KA78M05_TO252)`**: Nhận $`+8.0\,\text{V}`$ từ `U1` (độ chênh áp $`\Delta V = 8.0 - 5.0 = 3.0\,\text{V} \gt V_{\text{drop}} \approx 2.0\,\text{V}`$, đủ biên ổn áp khi gánh thêm tải `+3.3V`), ổn áp xuống $`+5.00\,\text{V}`$ ($`\pm 2\%`$), đi qua mắt lọc thông thấp $`LC`$ gồm `L6 = 100uH` và `C20 = 100uF` (kèm `C22 = 100nF`). Tần số cắt của mắt lọc $`L_6 C_{20}`$ là:

  ```math
  f_{c(+5\text{V})} = \frac{1}{2\pi\sqrt{L_6 C_{20}}} = \frac{1}{2\pi\sqrt{100 \times 10^{-6} \times 100 \times 10^{-6}}} \approx 1.59\,\text{kHz}
  ```

  Tại tần số đóng cắt của bộ nguồn xung ($`f_{\text{sw}} \approx 500\,\text{kHz}\text{–}1.5\,\text{MHz}`$), mắt lọc $`L_6 C_{20}`$ suy giảm gợn sóng thêm hơn $`-80\,\text{dB}`$.
- **`U2 (LM1117MP-3.3)`**: Hạ áp từ $`+5.0\,\text{V}`$ xuống $`+3.301\,\text{V}`$ (đo thực tế $`V_{\text{REF}} = 3.301\,\text{V}`$), độ sụt áp chênh lệch $`\Delta V = 5.0 - 3.3 = 1.7\,\text{V} \gt V_{\text{dropout}} \approx 1.1\,\text{V}`$, đảm bảo ổn áp tuyến tính hoàn toàn cho `YD-RP2040` và mức kẹp dương của `D8 (BAT54S)`.

#### d) Tầng ổn áp tuyến tính âm `−5V` dùng `Q1 (PNP) + D5 (Zener 5.6V) + R43 (1k)`
- **Cấu tạo trên schematic chính thức**:
  - Cực phát (`Emitter - E`) của `Q1 (PNP)` nối với đường `-6V` (hoặc phía nguồn âm trước lọc).
  - Diode Zener `D5 (5.6V)` nối giữa `GNDREF` (`D5/1`) và cực gốc `Base (B)` của `Q1` (`D5/2`), được phân cực về `-6V` qua điện trở `R43 = 1k`.
  - Điện áp tại cực gốc `B` của `Q1` được ghim ở mức $`-V_{Z(D5)} \approx -5.6\,\text{V}`$ (dòng phân cực qua `R43` là $`I_{R43} = \frac{6.0 - 5.6}{1\,\text{k}\Omega} \approx 0.4\,\text{mA}`$, hoặc khi nguồn âm trước ổn áp ở mức $`-6.8\,\text{V}`$ thì $`I_{R43} \approx 1.2\,\text{mA}`$).
  - Khi transistor thuận `Q1 (PNP)` dẫn trong vùng tích cực, mối nối Emitter–Base phân cực thuận với $`V_{EB} \approx 0.6\,\text{V}`$, giữ điện áp nhánh âm sau ổn áp (lọc qua `L7 = 100uH`, `C21 = 100uF`, `C23 = 100nF`) ổn định ở:

    ```math
    V_{-5\text{V}} = -V_{Z(D5)} + V_{EB(Q1)} \approx -5.6\,\text{V} + 0.6\,\text{V} = -5.00\,\text{V}
    ```

  - **Ưu điểm so với `MC79M05`**: IC `MC79M05` đòi hỏi điện áp rơi tối thiểu (dropout) từ $`1.1\,\text{V}`$ đến $`2.0\,\text{V}`$ (cần đầu vào thấp hơn $`-6.5\,\text{V}`$ đến $`-7.0\,\text{V}`$), trong khi cấu trúc `Q1 (PNP) + D5 (5.6V)` có $`V_{\text{drop}} \approx 0.6\,\text{V}`$ nên hoạt động tốt ngay cả khi đầu vào chỉ ở mức $`-6.0\,\text{V}`$ với dòng tiêu thụ của riêng `TL084` chỉ khoảng $`I_{CC} \approx 5.6\,\text{mA}`$.

---

### 1.3. Tính toán Khối ghép nối (`SW1`) & Cầu phân áp bù tần số (`SW2`)

#### a) Tụ ghép nối xoay chiều `C1 = 100nF` (Chế độ `AC` trên `SW1`)
- Khi `SW1` gạt sang vị trí `AC`, tụ `C1 = 100nF` mắc nối tiếp với trở kháng vào của cầu phân áp `SW2` ($`Z_{\text{in}} \approx 2.0\,\text{M}\Omega`$ ở thang `100mV` và `1V`, hoặc $`100\,\text{k}\Omega`$ ở thang `10mV` khi xét tần số thấp).
- Tần số cắt dưới (High-Pass Filter) ở thang `100mV` và `1V` là:

  ```math
  f_{c,\text{AC}} = \frac{1}{2\pi \cdot R_{\text{in}} \cdot C_1} = \frac{1}{2\pi \times 2.0 \times 10^6 \times 100 \times 10^{-9}} \approx 0.796\,\text{Hz}
  ```

  Như vậy, mọi tín hiệu xoay chiều có tần số từ vài Hz trở lên đều đi qua không suy hao, trong khi thành phần một chiều ($`f = 0\,\text{Hz}`$) bị chặn hoàn toàn.

#### b) Trọng số suy hao ($`\alpha_1`$) và điều kiện bù phẳng tần số của 3 nhánh `SW2`

1. **Nhánh `/10mV` (`SW2` Pin 1)**:
   - **Linh kiện**: $`R_{17} = 100\,\text{k}\Omega`$ mắc song song với $`C_{24} = 200\,\text{pF}`$, dẫn thẳng vào ngõ vào JFET `U6A+` (chân 3, $`R_{\text{in}(U6A)} \approx 10^{12}\,\Omega`$, $`C_{\text{in}(U6A)} \approx 3\text{–}5\,\text{pF}`$).
   - **Hệ số truyền đạt một chiều** ($`\alpha_{1,10\text{mV}}`$):

     ```math
     \alpha_{1(10\text{mV})} = \frac{R_{\text{in}(U6A)}}{R_{17} + R_{\text{in}(U6A)}} \approx 1.0000
     ```

   - **Vai trò của** $`R_{17} \parallel C_{24}`$: $`R_{17} = 100\,\text{k}\Omega`$ đóng vai trò điện trở hạn dòng bảo vệ cửa cực JFET của `U6A` khi người dùng đo nhầm điện áp lớn ở thang nhạy nhất (`10mV`), còn $`C_{24} = 200\,\text{pF}`$ ($`\gg C_{\text{in}(U6A)}`$) tạo đường dẫn dung kháng thấp cho các hài tần số cao của xung vuông không bị suy giảm bởi mắt lọc $`R_{17} \cdot C_{\text{in}(U6A)}`$.

2. **Nhánh `/100mV` (`SW2` Pin 2)**:
   - **Linh kiện**:
     - Nhánh trên: $`R_{18} = 1.8\,\text{M}\Omega\,(1\%)`$ mắc song song với $`C_{25} = 3\,\text{pF}`$.
     - Nhánh dưới (nối `S_GND`): $`R_{21} = 200\,\text{k}\Omega\,(1\%)`$ mắc song song với $`C_{29} = 15\,\text{pF}`$ và điện dung ký sinh của đường mạch + chân 3 `U6A` ($`C_{\text{stray}} \approx 12\,\text{pF}`$).
   - **Tổng trở kháng đầu vào**:

     ```math
     Z_{\text{in}(100\text{mV})} = R_{18} + R_{21} = 1.8\,\text{M}\Omega + 0.2\,\text{M}\Omega = 2.00\,\text{M}\Omega
     ```

   - **Trọng số phân áp thuần trở** ($`\alpha_{1,100\text{mV}}`$):

     ```math
     \alpha_{1(100\text{mV})} = \frac{R_{21}}{R_{18} + R_{21}} = \frac{200\,\text{k}\Omega}{1800\,\text{k}\Omega + 200\,\text{k}\Omega} = \frac{200}{2000} = 0.1000 \text{ (Tỷ lệ } 1:10\text{)}
     ```

   - **Kiểm chứng cân bằng hằng số thời gian bù tần số**:
     - Hằng số thời gian nhánh trên:

       ```math
       \tau_{\text{trên}(100\text{mV})} = R_{18} \cdot C_{25} = 1.8 \times 10^6\,\Omega \times 3 \times 10^{-12}\,\text{F} = 5.40\,\mu\text{s}
       ```

     - Điện dung tổng nhánh dưới cần thiết để bù phẳng tuyệt đối:

       ```math
       C_{\text{dưới,cần}(100\text{mV})} = \frac{R_{18}}{R_{21}} \cdot C_{25} = \frac{1800\,\text{k}\Omega}{200\,\text{k}\Omega} \times 3\,\text{pF} = 9 \times 3\,\text{pF} = 27.0\,\text{pF}
       ```

     - Với tụ gốm gắn cố định $`C_{29} = 15\,\text{pF}`$ cộng điện dung ký sinh tự nhiên của đường mạch phíp đồng và ngõ vào `U6A+` ($`C_{\text{stray}} \approx 12\,\text{pF}`$):

       ```math
       C_{\text{dưới,thực}} = C_{29} + C_{\text{stray}} = 15\,\text{pF} + 12\,\text{pF} = 27.0\,\text{pF} \implies \tau_{\text{dưới}(100\text{mV})} = 200\,\text{k}\Omega \times 27\,\text{pF} = 5.40\,\mu\text{s}
       ```

       Hai hằng số thời gian khớp nhau hoàn toàn ($`\tau_{\text{trên}} = \tau_{\text{dưới}} = 5.40\,\mu\text{s}`$), giúp tỷ số chia ở cao tần $`\frac{C_{25}}{C_{25} + C_{29} + C_{\text{stray}}} = \frac{3}{3 + 27} = 0.1000`$ bằng đúng tỷ số chia một chiều!

3. **Nhánh `/1V` (`SW2` Pin 4)**:
   - **Linh kiện (cập nhật theo schematic bản chính thức)**:
     - Nhánh trên: $`R_{19} = 2.0\,\text{M}\Omega\,(1\%)`$ mắc song song với $`C_{26} = 6\,\text{pF}`$.
     - Nhánh dưới (nối `S_GND`): $`R_{20} = 20\,\text{k}\Omega\,(1\%)`$ mắc song song với cặp tụ gốm cố định $`C_{27} = 120\,\text{pF}`$ và $`C_{28} = 470\,\text{pF}`$ cùng điện dung ký sinh $`C_{\text{stray}} \approx 10\,\text{pF}`$.
   - **Tổng trở kháng đầu vào**:

     ```math
     Z_{\text{in}(1\text{V})} = R_{19} + R_{20} = 2.00\,\text{M}\Omega + 0.02\,\text{M}\Omega = 2.02\,\text{M}\Omega
     ```

   - **Trọng số phân áp thuần trở** ($`\alpha_{1,1\text{V}}`$):

     ```math
     \alpha_{1(1\text{V})} = \frac{R_{20}}{R_{19} + R_{20}} = \frac{20\,\text{k}\Omega}{2000\,\text{k}\Omega + 20\,\text{k}\Omega} = \frac{20}{2020} = \frac{1}{101} \approx 0.00990099
     ```

     > **Chú ý:** Đây chính là lý do trong `Form1.cs` dòng 622, trọng số `alpha1` của thang `1V` được viết chính xác là `20.0 / 2020.0` thay vì `0.01`, giúp loại bỏ $`1\%`$ sai số hệ thống!
   - **Kiểm chứng cân bằng hằng số thời gian bù tần số**:
     - Hằng số thời gian nhánh trên:

       ```math
       \tau_{\text{trên}(1\text{V})} = R_{19} \cdot C_{26} = 2.0 \times 10^6\,\Omega \times 6 \times 10^{-12}\,\text{F} = 12.00\,\mu\text{s}
       ```

     - Điện dung tổng nhánh dưới cần thiết để bù phẳng ($`R_{19}/R_{20} = 2000\,\text{k}\Omega / 20\,\text{k}\Omega = 100`$):

       ```math
       C_{\text{dưới,cần}(1\text{V})} = \frac{R_{19}}{R_{20}} \cdot C_{26} = 100 \times 6\,\text{pF} = 600.0\,\text{pF}
       ```

     - Tổng điện dung nhánh dưới trên mạch chính thức:

       ```math
       \begin{aligned}
       C_{\text{dưới,thực}} &= C_{27} + C_{28} + C_{\text{stray}} = 120\,\text{pF} + 470\,\text{pF} + 10\,\text{pF} = 600.0\,\text{pF} \\
       \implies \tau_{\text{dưới}(1\text{V})} &= R_{20} \cdot C_{\text{dưới,thực}} = 20\,\text{k}\Omega \times 600\,\text{pF} = 12.00\,\mu\text{s}
       \end{aligned}
       ```

     - **Ý nghĩa thiết kế**: Khi nâng $`C_{26}`$ từ $`1\,\text{pF}`$ lên $`6\,\text{pF}`$ và nâng tụ nhánh dưới lên $`120\,\text{pF} + 470\,\text{pF} = 590\,\text{pF}`$, điện dung ký sinh $`C_{\text{stray}} \approx 10\,\text{pF}`$ của phíp đồng chỉ còn chiếm $`1.67\%`$ tổng điện dung nhánh dưới, giúp mạch cực kỳ ổn định và hầu như không bị lệch bù bởi dung sai phíp mạch!

---

### 1.4. Tính toán Tầng đệm `U6A` và Bộ chia tinh `SW3` ($`\alpha_2`$)

1. **Tầng đệm `U6A` (`TL084` chân 3+, chân 2−, chân 1 Out)**:
   - Chân 2 (`U6A−`) nối trực tiếp với chân 1 (`U6A Out`, mạng `/X1`), tạo thành mạch khuếch đại lặp điện áp (Voltage Follower):

     ```math
     A_{v(U6A)} = 1.0000, \quad BW_{U6A} \approx \text{GBWP} = 3.0\,\text{MHz}
     ```

   - Trở kháng vào $`Z_{\text{in}(U6A)} \approx 10^{12}\,\Omega`$ không gây tải lên cầu chia `SW2`, trong khi trở kháng ra vòng kín $`Z_{\text{out}(U6A)} \lt 1\,\Omega`$ đủ sức kéo mạng điện trở thấp của `SW3`.
   - **Tại sao bỏ `D10/D11` trước chân 3 `U6A+`**: Tín hiệu tại chân 3 `U6A+` là tín hiệu xoay chiều lưỡng cực dao động quanh $`0\,\text{V}`$ (có cả bán kỳ dương lẫn bán kỳ âm), trong khi `U6A` được nuôi bằng nguồn đối xứng $`\pm 5\,\text{V}`$. Nếu gắn diode kẹp về `GND` tại chân 3, mọi bán kỳ âm dưới $`-0.3\,\text{V}`$ sẽ bị diode dẫn ghim mất. Do đó, việc bỏ `D10/D11` trên schematic chính thức là hoàn toàn chính xác.

2. **Mạng chia tinh `SW3` (`R26 = 300Ω`, `R27 = 180Ω`, `R28 = 120Ω`)**:
   - Ba điện trở mắc nối tiếp từ ngõ ra chân 1 của `U6A` (mạng `/X1`) xuống `S_GND`:

     ```math
     R_{\text{tổng}(SW3)} = R_{26} + R_{27} + R_{28} = 300 + 180 + 120 = 600\,\Omega
     ```

   - Dòng tải cực đại mà `U6A` phải cấp cho chuỗi `SW3` (khi đỉnh sóng tại `/X1` đạt $`\pm 1.8\,\text{V}`$) chỉ là $`I_{\text{load}} = \frac{1.8\,\text{V}}{600\,\Omega} = 3.0\,\text{mA}`$, nằm sâu trong vùng làm việc tuyến tính của `TL084`.
   - **Trọng số suy hao** $`\alpha_2`$ **tại 3 vị trí của `SW3A`**:
     - **Vị trí `/X1` (`SW3` Pin 1, lấy trực tiếp tại đầu trên của `R26`)**:

       ```math
       \alpha_{2(\text{X1})} = \frac{R_{26} + R_{27} + R_{28}}{R_{\text{tổng}}} = \frac{600}{600} = 1.0000
       ```

     - **Vị trí `/X2` (`SW3` Pin 2, lấy giữa `R26` và `R27`)**:

       ```math
       \alpha_{2(\text{X2})} = \frac{R_{27} + R_{28}}{R_{26} + R_{27} + R_{28}} = \frac{180 + 120}{600} = \frac{300}{600} = 0.5000 \text{ (Chia } 2\text{)}
       ```

     - **Vị trí `/X5` (`SW3` Pin 4, lấy giữa `R27` và `R28`)**:

       ```math
       \alpha_{2(\text{X5})} = \frac{R_{28}}{R_{26} + R_{27} + R_{28}} = \frac{120}{600} = 0.2000 \text{ (Chia } 5\text{)}
       ```

---

### 1.5. Tính toán Chi tiết Chuỗi Khuếch đại 2 Tầng Không Đảo `U6B` & `U6C` và Dịch mức DC

Đây là phần tính toán trọng tâm nhất của khối AFE trên sơ đồ chính thức:

#### a) Tầng khuếch đại thứ nhất `U6B` (`TL084` chân 5+, chân 6−, chân 7 Out)
- Tín hiệu sau `SW3A` đi vào ngõ không đảo chân 5 (`U6B+`): $`V_5 = V_{\text{in}} \cdot \alpha_1 \cdot \alpha_2`$.
- Mạng hồi tiếp âm tại chân 6 (`U6B−`) gồm `R41 = 1k 1%` nối từ ngõ ra chân 7 về chân 6 và `R42 = 560Ω 1%` nối từ chân 6 xuống `S_GND`.
- **Hệ số khuếch đại không đảo của `U6B`**:

  ```math
  A_{v1(U6B)} = 1 + \frac{R_{41}}{R_{42}} = 1 + \frac{1000}{560} = 1 + \frac{25}{14} = \frac{39}{14} \approx 2.785714\,\text{lần}
  ```

- **Băng thông vòng kín của tầng `U6B`** (với $`\text{GBWP}_{\text{TL084}} = 3.0\,\text{MHz}`$):

  ```math
  BW_{U6B} = \frac{\text{GBWP}}{A_{v1(U6B)}} = \frac{3.0\,\text{MHz}}{2.785714} \approx 1.0769\,\text{MHz} \,(1076.9\,\text{kHz})
  ```

#### b) Tầng khuếch đại thứ hai kết hợp dịch mức một chiều `U6C` (`TL084` chân 10+, chân 9−, chân 8 Out)
- Ngõ ra chân 7 của `U6B` nối trực tiếp vào ngõ không đảo chân 10 (`U6C+`): $`V_{10} = A_{v1} \cdot V_5`$.
- Tại nút đảo chân 9 (`U6C−`), có 3 điện trở hội tụ:
  - `R34 = 1k 1%` nối hồi tiếp từ ngõ ra chân 8 (`U6C Out`) về chân 9.
  - `R33 = 560Ω 1%` nối từ chân 9 xuống `S_GND` ($`0\,\text{V}`$).
  - `R35 = 3k` nối từ chân 9 về nguồn âm $`V_{EE} = -5.00\,\text{V}`$.
- **Thiết lập phương trình nút Kirchhoff (KCL) tại chân 9 (`U6C−`)**:
  Do khuếch đại thuật toán làm việc trong vùng hồi tiếp âm tuyến tính nên $`V_9 = V_{10}`$ và dòng vào chân 9 bằng $`0`$:

  ```math
  \frac{V_8 - V_{10}}{R_{34}} = \frac{V_{10} - 0}{R_{33}} + \frac{V_{10} - (-5.00\,\text{V})}{R_{35}}
  ```

  Nhân hai vế với $`R_{34}`$ và chuyển vế tìm điện áp ngõ ra $`V_8 = V_{\text{out}(U6C)}`$:

  ```math
  V_8 = V_{10} \cdot \left(1 + \frac{R_{34}}{R_{33}} + \frac{R_{34}}{R_{35}}\right) + 5.00\,\text{V} \cdot \left(\frac{R_{34}}{R_{35}}\right)
  ```

- **Hệ số khuếch đại tín hiệu xoay chiều của tầng `U6C`** ($`A_{v2}`$):

  ```math
  A_{v2(U6C)} = 1 + \frac{R_{34}}{R_{33}} + \frac{R_{34}}{R_{35}} = 1 + \frac{1000}{560} + \frac{1000}{3000} = 1 + 1.785714 + 0.333333 = \frac{131}{42} \approx 3.119048\,\text{lần}
  ```

- **Băng thông vòng kín của tầng `U6C`**:

  ```math
  BW_{U6C} = \frac{\text{GBWP}}{A_{v2(U6C)}} = \frac{3.0\,\text{MHz}}{3.119048} \approx 961.83\,\text{kHz}
  ```

- **Điện áp dịch mức một chiều (DC Offset) tại ngõ ra `U6C`** (khi $`V_{\text{in}} = 0\,\text{V} \implies V_{10} = 0\,\text{V}`$):

  ```math
  V_{\text{offset}} = \lvert -5.00\,\text{V} \rvert \cdot \frac{R_{34}}{R_{35}} = 5.00\,\text{V} \times \frac{1000\,\Omega}{3000\,\Omega} = +1.6667\,\text{V}
  ```

  > **Lưu ý quan trọng:** $`V_{\text{offset}}`$ chỉ phụ thuộc vào tỷ số $`R_{34}/R_{35}`$ và nguồn $`-5\,\text{V}`$, hoàn toàn độc lập với $`R_{33}`$! Khi nguồn $`-5\,\text{V}`$ thực tế đạt khoảng $`-4.95\,\text{V}`$ đến $`-5.02\,\text{V}`$, điểm tĩnh đo được tại `/ADC_in` nằm trong khoảng $`+1.650\,\text{V}`$ đến $`+1.675\,\text{V}`$, khớp tuyệt đối với hằng số `ADC_MID_VOLT = 1.650` trong `Form1.cs`.

#### c) Tổng độ lợi hai tầng `U6B × U6C` (`AFE_AMP_GAIN`) và băng thông toàn chuỗi
- **Tổng hệ số khuếch đại không đảo của cụm `U6B – U6C`**:

  ```math
  A_{v,\text{total}} = A_{v1(U6B)} \times A_{v2(U6C)} = \frac{39}{14} \times \frac{131}{42} = \frac{5109}{588} \approx 8.688776\,\text{lần}
  ```

  Trong `Form1.cs` (dòng 18), hằng số này được khai báo chính xác bằng biểu thức:

  ```csharp
  private const double AFE_AMP_GAIN = (1.0 + 1000.0 / 560.0) * (1.0 + 1000.0 / 560.0 + 1000.0 / 3000.0); // = 8.68877551...
  ```
- **Băng thông tổng hợp** ($`-3\,\text{dB}`$) **của 2 tầng khuếch đại bậc 1 mắc nối tiếp**:
  Với $`f_1 = 1076.9\,\text{kHz}`$ và $`f_2 = 961.8\,\text{kHz}`$, băng thông tổng hợp của chuỗi 2 tầng xấp xỉ:

  ```math
  BW_{\text{cascade}} \approx \frac{1}{\sqrt{\frac{1}{f_1^2} + \frac{1}{f_2^2}}} \approx 655\text{–}715\,\text{kHz}
  ```

  Cao gần **gấp đôi** so với phương án dồn toàn bộ độ lợi $`8.69\times`$ vào một tầng đơn lẻ ($`BW_{\text{1-stage}} = \frac{3000\,\text{kHz}}{8.6888} \approx 345\,\text{kHz}`$)!

#### d) Phương trình tổng quát quy đổi điện áp từ `V_in` sang `V_ADC` và ngược lại
- **Chiều thuận** (từ đầu vào BNC $`V_{\text{in}}`$ tới chân `GPIO26 / ADC0`):

  ```math
  V_{\text{ADC}} = V_{\text{offset}} + V_{\text{in}} \cdot \alpha_1 \cdot \alpha_2 \cdot A_{v,\text{total}} = 1.650 + V_{\text{in}} \cdot \alpha_1 \cdot \alpha_2 \cdot 8.6888
  ```

- **Chiều nghịch** (khôi phục điện áp thực $`V_{\text{in}}`$ trên phần mềm WinForms `Form1.cs`):

  ```math
  V_{\text{in}} = \frac{V_{\text{ADC}} - 1.650}{8.6888 \cdot \alpha_1 \cdot \alpha_{2,\text{hw}}}
  ```

- **Bảng tổng hợp trọng số toàn dải đo**:

| Thang `SW2` (`SEN1`) | Trọng số $`\alpha_1`$ | Thang `SW3` (`SEN2`) | Trọng số $`\alpha_2`$ | Độ lợi tổng $`G_{\text{total}} = 8.6888 \cdot \alpha_1 \cdot \alpha_2`$ | Dải đo toàn màn hình ($`\pm 4\,\text{div}`$) | Biên độ cực đại không bão hòa ADC ($`V_{\text{ADC}} \in [0; 3.3\,\text{V}]`$) |
| :--- | :---: | :---: | :---: | :---: | :---: | :---: |
| **`1V`** | $`\frac{20}{2020} \approx 0.009901`$ | **`X5`** | $`0.200`$ | $`0.017205\,\text{V/V}`$ | $`\pm 20\,\text{V}`$ ($`5\,\text{V/div}`$) | $`\pm 95.9\,\text{V}`$ |
| **`1V`** | $`\frac{20}{2020} \approx 0.009901`$ | **`X2`** *(PM)* | $`0.500`$ | $`0.043014\,\text{V/V}`$ *(cứng)* | $`\pm 8\,\text{V}`$ ($`2\,\text{V/div}`$) | $`\pm 38.4\,\text{V}`$ |
| **`1V`** | $`\frac{20}{2020} \approx 0.009901`$ | **`X1`** | $`1.000`$ | $`0.086027\,\text{V/V}`$ | $`\pm 4\,\text{V}`$ ($`1\,\text{V/div}`$) | $`\pm 19.2\,\text{V}`$ |
| **`100mV`** (`0.1V`) | $`0.100000`$ | **`X5`** | $`0.200`$ | $`0.173776\,\text{V/V}`$ | $`\pm 2.0\,\text{V}`$ ($`0.5\,\text{V/div}`$) | $`\pm 9.50\,\text{V}`$ |
| **`100mV`** (`0.1V`) | $`0.100000`$ | **`X1`** | $`1.000`$ | $`0.868878\,\text{V/V}`$ | $`\pm 0.4\,\text{V}`$ ($`0.1\,\text{V/div}`$) | $`\pm 1.90\,\text{V}`$ |
| **`10mV`** | $`1.000000`$ | **`X5`** | $`0.200`$ | $`1.737755\,\text{V/V}`$ | $`\pm 200\,\text{mV}`$ ($`50\,\text{mV/div}`$) | $`\pm 950\,\text{mV}`$ |
| **`10mV`** | $`1.000000`$ | **`X1`** | $`1.000`$ | $`8.688776\,\text{V/V}`$ | $`\pm 40\,\text{mV}`$ ($`10\,\text{mV/div}`$) | $`\pm 190\,\text{mV}`$ |

---

### 1.6. Tính toán Khối lọc RC, Kẹp áp bảo vệ ADC, Mạch Schmitt Trigger `U6D` và Mạng đọc công tắc

1. **Mắt lọc thông thấp chống chồng phổ / nhiễu gai trước ADC (`R36 = 120Ω`, `C30 = 120pF`)**:
   - Tần số cắt thông thấp:

     ```math
     f_{c(\text{ADC})} = \frac{1}{2\pi R_{36} C_{30}} = \frac{1}{2\pi \times 120\,\Omega \times 120 \times 10^{-12}\,\text{F}} \approx 11.05\,\text{MHz}
     ```

   - Vai trò kép: (1) `C30 = 120pF` đóng vai trò bể chứa điện tích tức thời (charge reservoir) cung cấp dòng nạp nhanh cho tụ lấy mẫu nội ($`C_{\text{S\&H}} \approx 1\,\text{pF}`$) của SAR ADC trên RP2040 khi lấy mẫu ở tốc độ $`3.00\,\text{MSa/s}`$; (2) `R36 = 120Ω` giới hạn dòng qua cặp diode Schottky `D8` (kẹp lên `+3V3`) và `D9` (kẹp xuống `S_GND`) ở mức an toàn:

     ```math
     I_{\text{clamp,max}} \approx \frac{3.8\,\text{V} - 3.3\,\text{V}}{120\,\Omega} \approx 4.2\,\text{mA} \ll I_{F(\text{BAT54S})} = 200\,\text{mA}
     ```

2. **Mạch lọc PWM tạo ngưỡng Trigger (`R39 = 100kΩ`, `C31 = 100nF`) và Schmitt Trigger `U6D`**:
   - Chân `GPIO20` (`/Trig_PWM`) xuất xung PWM tần số $`f_{\text{PWM}} = 100\,\text{kHz}`$, hệ số lấp đầy $`D = 50\%`$.
   - Mắt lọc thông thấp `R39 = 100kΩ`, `C31 = 100nF` có tần số cắt:

     ```math
     f_{c(\text{TrigPWM})} = \frac{1}{2\pi R_{39} C_{31}} = \frac{1}{2\pi \times 100 \times 10^3 \times 100 \times 10^{-9}} \approx 15.92\,\text{Hz}
     ```

     Tại $`f_{\text{PWM}} = 100\,\text{kHz}`$ (cao gấp $`6280`$ lần tần số cắt), gợn sóng xoay chiều bị suy giảm xuống dưới $`0.5\,\text{mV}`$, tạo ra điện áp chuẩn một chiều phẳng tại chân 13 (`U6D−`):

     ```math
     V_{\text{ref}(U6D-)} = D \cdot V_{\text{IO}} = 0.50 \times 3.301\,\text{V} \approx +1.6505\,\text{V}
     ```

   - Tại ngõ vào không đảo chân 12 (`U6D+`), điện trở vào `R37 = 120Ω` và điện trở hồi tiếp dương `R38 = 100kΩ` (từ chân 14 về chân 12) tạo dải trễ Schmitt (Hysteresis) chống rung xung khi tín hiệu đi qua ngưỡng:

     ```math
     \Delta V_{\text{hyst}(U6D)} = (V_{\text{OH}} - V_{\text{OL}}) \cdot \frac{R_{37}}{R_{37} + R_{38}} \approx 7.5\,\text{V} \times \frac{120}{100120} \approx 9.0\,\text{mV}
     ```

   - Ngõ ra chân 14 của `U6D` đi qua `R40 = 1k`, diode chỉnh lưu nửa chu kỳ dương `D7 (1N4148W)` (chặn hoàn toàn mức bão hòa âm $`-3.8\,\text{V}`$ của `U6D`) và điện trở phân áp xuống đất `R53 = 1k` để hạ mức đỉnh dương về khoảng $`\frac{3.8 - 0.6}{2} \approx +1.6\,\text{V} \div +2.0\,\text{V}`$ an toàn cho chân `GPIO21` (`/Trigger`).

3. **Mạng phân áp 3 mức đọc trạng thái công tắc `SW1B`, `SW2B`, `SW3B`**:
   - Mỗi công tắc sử dụng cặp điện trở $`10\,\text{k}\Omega`$ mắc nối tiếp từ nguồn `+5V` xuống `S_GND` (ví dụ `R16 = 10k`, `R14 = 10k`, `R15 = 10k` tại `SW1B`), nhưng điểm giữa được kẹp bởi diode bảo vệ nội / tải hoặc tạo ra 3 vùng điện áp phân biệt rõ rệt đưa qua điện trở nối tiếp $`1\,\text{k}\Omega`$ (`R13`, `R25`, `R32`) vào `GPIO28 (ADC2)`, `GPIO27 (ADC1)` và `GPIO22`:
     - Vị trí nối đất (`Pin 8 → S_GND`): $`V \approx 0.00\,\text{V} \le 0.5\,\text{V} \implies \text{Mã } 0`$ (`GND` trên `SW1`, `1V` trên `SW2`, `X5` mức thấp trên `SW3`).
     - Vị trí giữa (`Pin 6`): $`V \approx 1.65\,\text{V} \in [1.5\,\text{V}; 1.8\,\text{V}] \implies \text{Mã } 1`$ (`AC` trên `SW1`, `100mV` trên `SW2`).
     - Vị trí trên (`Pin 5`): $`V \ge 2.7\,\text{V} \implies \text{Mã } 2`$ (`DC` trên `SW1`, `10mV` trên `SW2`, `X1` mức cao trên `SW3`).

---

## PHẦN 2: GIẢI NGHĨA CHI TIẾT MÃ NGUỒN FIRMWARE `rp2040_dso_dma.ino`

Tệp `firmware_winform_arduino/rp2040_dso_dma/rp2040_dso_dma.ino` (`271` dòng) được viết bằng C/C++ trên nền tảng **Earle F. Philhower `arduino-pico`**, gọi trực tiếp các thanh ghi phần cứng của **Raspberry Pi Pico C/C++ SDK** (`hardware/adc.h`, `hardware/dma.h`, `hardware/clocks.h`, `hardware/pwm.h`, `hardware/vreg.h`).

### 2.1. Các hằng số cấu hình và biến trạng thái toàn cục (Dòng `1–37`)

| Định nghĩa / Biến | Giá trị | Ý nghĩa và chức năng kỹ thuật |
| :--- | :--- | :--- |
| `PIN_ADC_IN` / `ADC_CHANNEL` | `26` / `0` | Chân `GPIO26` (kênh `ADC0`) nhận tín hiệu tương tự `/ADC_in` sau tầng `U6C`. |
| `PIN_ADC_VOL` | `27` (`ADC1`) | Chân `GPIO27` đọc điện áp 3 mức `/Vol_sel` từ công tắc thang đo `SW2B`. |
| `PIN_ADC_MODE` | `28` (`ADC2`) | Chân `GPIO28` đọc điện áp 3 mức `/Mode_sel` từ công tắc ghép nối `SW1B`. |
| `PIN_SW3` | `22` | Chân số `GPIO22` đọc mức logic `/Scope_sel` từ công tắc `SW3B` (`1 = X1`, `0 = X5`). |
| `PIN_TEST_PWM` | `0` | Chân `GPIO0` xuất xung vuông kiểm tra có thể chỉnh tần số (`10..500.000 Hz`) và Duty (`1..99%`). |
| `PIN_TRIG_PWM` | `20` | Chân `GPIO20` xuất xung PWM `100 kHz`, `50%` duty tạo áp chuẩn `~1.65V` cho `U6D−`. |
| `NUM_SAMPLES` | `1024` | Số lượng mẫu ADC thu thập trong mỗi khung truyền (`1024 mẫu × 2 byte = 2048 byte`). |
| `FRAME_HEADER_SIZE` / `FRAME_SIZE` | `4` / `2053` | Khung 2053 byte = `4 byte Header` (`0xAA, 0x55, rateCode, swByte`) + `2048 byte dữ liệu` + `1 byte XOR`. |
| `FRAME_INTERVAL_MS` | `33` | Chu kỳ phát khung dữ liệu lên máy tính (`33 ms` $`\approx 30.3\,\text{FPS}`$). |
| `RATE_CLKDIV[9]` | `{0, 191, 479, 959, 2399, 4799, 0, 0, 0}` | Hệ số chia xung nhịp ADC (`clkdiv`). Công thức phần cứng RP2040: $`F_s = \frac{f_{\text{clk\_adc}}}{96 \times (1 + \text{clkdiv})}`$. Khi `clkdiv = 0`, bộ đếm chạy liên tục mỗi 96 chu kỳ một mẫu. |
| `adcBuf[1024]` / `txFrame[2053]` | Bộ đệm RAM tĩnh | `adcBuf` là vùng đích để kênh DMA ghi trực tiếp 1024 mẫu 16-bit; `txFrame` là gói nhị phân hoàn chỉnh gửi qua `Serial.write()`. |

### 2.2. Giải nghĩa từng hàm chức năng trong `rp2040_dso_dma.ino`

#### 1) `initPwmPin(uint pin, float clkDiv, uint16_t wrap, uint16_t level)` (Dòng `38–46`)
- **Chức năng**: Cấu hình phần cứng một chân GPIO sang chức năng băm xung `GPIO_FUNC_PWM`.
- **Cơ chế**: Tìm bộ tạo xung (`slice`) tương ứng với chân GPIO bằng `pwm_gpio_to_slice_num(pin)`, nạp hệ số chia tần số bộ đếm `clkDiv` (kiểu dấu phẩy động 8.4-bit) và giá trị đếm tràn `wrap` (16-bit, từ `0` đến `65535`), kích hoạt bộ đếm và đặt ngưỡng so sánh `level` để quyết định độ rộng xung mức cao.

#### 2) `applyGenPwm(uint32_t freqHz, uint8_t dutyPct)` (Dòng `48–70`)
- **Chức năng**: Tính toán tự động cặp thông số `(clkDiv, wrap, level)` để phát xung vuông kiểm tra trên chân `GPIO0` ở bất kỳ tần số nào từ `10 Hz` đến `500.000 Hz` và hệ số lấp đầy từ `1%` đến `99%`.
- **Thuật toán**:
  1. Đọc tần số xung nhịp hệ thống thực tế hiện hành `sysHz = clock_get_hz(clk_sys)` (có thể đang là `125 MHz` lúc mới khởi động hoặc `288 MHz` sau khi kích hoạt Boost).
  2. Tính tổng số chu kỳ máy trong 1 chu kỳ xung cần phát: $`\text{totalTicks} = \frac{f_{\text{sys}}}{f_{\text{PWM}}}`$.
  3. Tìm hệ số chia nhỏ nhất $`\text{div} = \max\left(1.0, \frac{\text{totalTicks}}{65536.0}\right)`$ sao cho giá trị đếm đỉnh $`\text{wrap} = \text{round}\left(\frac{\text{totalTicks}}{\text{div}}\right) \le 65536`$, giúp tận dụng tối đa độ phân giải 16-bit của bộ đếm PWM.
  4. Tính thanh ghi `wrapVal = wrap - 1` và mức ngưỡng `level = (wrap * dutyPct) / 100`, rồi gọi `initPwmPin(PIN_TEST_PWM, ...)`.

#### 3) `setupPwmOutputs()` (Dòng `72–77`)
- **Chức năng**: Khởi tạo đồng thời 2 nguồn xung PWM:
  - Chân `GPIO20` (`/Trig_PWM`): Đặt `clkDiv = 1.0f`, `trigWrap = (sysHz / 100000) - 1`, `level = (trigWrap + 1) / 2` để luôn duy trì đúng tần số `100 kHz`, duty `50%` bất kể `clk_sys` đang chạy ở `125 MHz` hay `288 MHz`.
  - Chân `GPIO0` (`PIN_TEST_PWM`): Gọi lại `applyGenPwm(genFreqHz, genDutyPct)` để đồng bộ tần số xung vuông kiểm tra.

#### 4) `ensureBoostPll()` (Dòng `79–87`)
- **Chức năng**: Nâng xung nhịp hệ thống `clk_sys` lên **`288 MHz`** và điện áp lõi lên **`1.20 V`** đúng một lần duy nhất khi người dùng chọn các thang thời gian tốc độ cao (`1–20 us/div`, mã `R6..R8`).
- **Trình tự an toàn**:
  1. Chuyển tạm nguồn xung `clk_adc` về `PLL_USB` (`48 MHz`) trước khi đổi tần số `PLL_SYS` để khối ADC không bị sốc xung nhịp trong lúc khóa pha.
  2. Gọi `vreg_set_voltage(VREG_VOLTAGE_1_20)` nâng điện áp cấp nguồn lõi RP2040 từ `1.10 V` lên `1.20 V` và chờ `delay(5)` ms cho điện áp ổn định.
  3. Gọi `set_sys_clock_khz(288000, true)` cấu hình vòng khóa pha hệ thống `PLL_SYS` lên đúng `288.00 MHz`.
  4. Đặt cờ `pllBoosted = true` và gọi lại `setupPwmOutputs()` để hiệu chỉnh lại bộ chia PWM trên `GPIO0` và `GPIO20` theo xung nhịp mới `288 MHz`.

#### 5) `applySampleRate(uint8_t code)` (Dòng `89–109`)
- **Chức năng**: Thay đổi tốc độ lấy mẫu của khối SAR ADC theo mã tốc độ `code` (`0..8`) nhận từ máy tính:
  - **Nhóm tốc độ chuẩn `code = 0..5` (`500 kSa/s` xuống `10 kSa/s`)**: Sử dụng nguồn xung chuẩn `PLL_USB = 48 MHz` và nạp hệ số chia từ mảng `RATE_CLKDIV[code]`:
    - `code = 0`: `clkdiv = 0` $`\implies F_s = \frac{48\,\text{MHz}}{96 \times 1} = 500\,\text{kSa/s}`$
    - `code = 1`: `clkdiv = 191` $`\implies F_s = \frac{48\,\text{MHz}}{96 \times 192} = 250\,\text{kSa/s}`$
    - `code = 2`: `clkdiv = 479` $`\implies F_s = \frac{48\,\text{MHz}}{96 \times 480} = 100\,\text{kSa/s}`$ (mặc định khi khởi động ứng với `500 us/div`)
    - `code = 3`: `clkdiv = 959` $`\implies F_s = \frac{48\,\text{MHz}}{96 \times 960} = 50\,\text{kSa/s}`$
    - `code = 4`: `clkdiv = 2399` $`\implies F_s = \frac{48\,\text{MHz}}{96 \times 2400} = 20\,\text{kSa/s}`$
    - `code = 5`: `clkdiv = 4799` $`\implies F_s = \frac{48\,\text{MHz}}{96 \times 4800} = 10\,\text{kSa/s}`$
  - **Nhóm tốc độ cao Boost `code = 6..8` (`1.0 / 1.5 / 3.0 MSa/s`)**:
    - Gọi `ensureBoostPll()` để đảm bảo `PLL_SYS` đang chạy ở `288 MHz`.
    - Dùng bộ chia phần cứng của bộ tạo xung nhịp `clock_configure(clk_adc, ..., 288000000, targetAdcHz)` để cấp xung nhịp trực tiếp cho khối ADC từ `PLL_SYS` với `clkdiv = 0`:
      - `code = 6`: `targetAdcHz = 96 MHz` (chia 3 từ `288 MHz`) $`\implies F_s = \frac{96\,\text{MHz}}{96} = 1.00\,\text{MSa/s}`$
      - `code = 7`: `targetAdcHz = 144 MHz` (chia 2 từ `288 MHz`) $`\implies F_s = \frac{144\,\text{MHz}}{96} = 1.50\,\text{MSa/s}`$ (dùng cho thang `20 us/div`)
      - `code = 8`: `targetAdcHz = 288 MHz` (chia 1 từ `288 MHz`) $`\implies F_s = \frac{288\,\text{MHz}}{96} = 3.00\,\text{MSa/s}`$ (dùng cho các thang `1, 2, 5, 10 us/div`)

#### 6) `setupAdcAndDma()` (Dòng `111–127`)
- **Chức năng**: Khởi tạo ngoại vi ADC, hàng đợi FIFO và cấp phát 1 kênh DMA phần cứng.
- **Chi tiết cấu hình**:
  - `adc_fifo_setup(true, true, 1, false, false)`:
    - `en = true`: Đẩy kết quả chuyển đổi vào FIFO 4 phần tử của ADC.
    - `dreq_en = true`: Bật tín hiệu yêu cầu DMA (`DREQ_ADC`) mỗi khi FIFO có ít nhất `dreq_thresh = 1` mẫu.
    - `err_in_fifo = false`, `byte_shift = false`: Giữ nguyên mẫu 12-bit (`0..4095`) nằm gọn trong từ 16-bit (`uint16_t`) để khớp với bộ giải mã Little-Endian trên WinForms.
  - Cấu hình kênh DMA (`dmaCfg`):
    - `DMA_SIZE_16`: Mỗi lượt truyền đúng 16-bit (`2 byte`).
    - `read_increment = false`: Địa chỉ nguồn cố định tại thanh ghi phần cứng `&adc_hw->fifo`.
    - `write_increment = true`: Địa chỉ đích tự động tăng dần qua từng phần tử của mảng `adcBuf[0..1023]`.
    - `DREQ_ADC`: Đồng bộ nhịp truyền DMA hoàn toàn theo xung hoàn tất chuyển đổi của phần cứng ADC.

#### 7) `read3LevelChannel(uint ch, uint8_t prev)` & `readSwitchByte()` (Dòng `129–179`)
- **Chức năng**: Quét trạng thái vật lý của 3 công tắc `SW1`, `SW2`, `SW3` định kỳ mỗi `60 ms` và đóng gói vào 1 byte `swByte`.
- **Các kỹ thuật chống nhiễu quan trọng**:
  1. **Hạ tạm xung nhịp ADC về `48 MHz`**: Nếu ADC đang ở chế độ Boost (`144` hoặc `288 MHz`), trở kháng nguồn lớn ($`10\,\text{k}\Omega`$) của cầu phân áp công tắc sẽ không kịp nạp đầy tụ giữ mẫu S&H. Vì vậy, `readSwitchByte()` tạm thời chuyển `clk_adc` về `48 MHz PLL_USB`, đọc xong hai công tắc `SW1` và `SW2` rồi mới khôi phục lại tốc độ Boost cho kênh `ADC0`.
  2. **Đọc xả tụ lấy mẫu (`(void)adc_read()`)**: Khi bộ dồn kênh (MUX) vừa chuyển từ `ADC0` sang `ADC1` hoặc `ADC2`, điện tích dư của kênh trước vẫn còn lưu trên tụ S&H; lệnh đọc bỏ mẫu đầu tiên giúp xả sạch điện tích dư này.
  3. **Bộ lọc trung vị 5 mẫu (Median-of-5) kèm vùng trễ**: Thu 5 mẫu liên tiếp `s[0..4]`, sắp xếp tăng dần và lấy mẫu đứng giữa `s[2]` quy đổi ra Volt:
     - $`v \le 0.5\,\text{V} \implies \text{trả về } 0`$
     - $`1.5\,\text{V} \le v \le 1.8\,\text{V} \implies \text{trả về } 1`$
     - $`v \ge 2.7\,\text{V} \implies \text{trả về } 2`$
     - Nếu rơi vào vùng chuyển tiếp giữa các nấc (do người dùng đang gạt dở tay), hàm giữ nguyên trạng thái `prev` trước đó để giao diện không bị nhảy loạn.
  4. **Cấu trúc bit của `swByte`**:

     ```math
     \text{swByte} = (\text{sw1} \mathbin{\&} \text{0x03}) \mid ((\text{sw2} \mathbin{\&} \text{0x03}) \ll 2) \mid ((\text{sw3} \mathbin{\&} \text{0x01}) \ll 4)
     ```

     - Bit `[1:0]`: `SW1` (`0 = GND`, `1 = AC`, `2 = DC`)
     - Bit `[3:2]`: `SW2` (`0 = 1V`, `1 = 100mV`, `2 = 10mV`)
     - Bit `[4]`: `SW3` (`1 = X1`, `0 = X5`)

#### 8) `readHostCommand()` (Dòng `181–208`)
- **Chức năng**: Đọc không chặn (non-blocking) các dòng lệnh ASCII kết thúc bằng `\n` từ WinForms gửi xuống qua cổng USB-CDC:
  - Lệnh `R<code>\n` (ví dụ `R8\n`): Gọi `applySampleRate(atoi(cmdBuf + 1))`.
  - Lệnh `G<freq>,<duty>\n` (ví dụ `G1000,50\n`): Tách dấu phẩy, lấy tần số `f` và độ rộng xung `d`, rồi gọi `applyGenPwm(f, d)`.

#### 9) `captureBlockDMA()` & `buildAndSendFrame(uint8_t rateCode, uint8_t swByte)` (Dòng `210–245`)
- **`captureBlockDMA()`**:
  1. Dừng ADC (`adc_run(false)`) và xả sạch mọi mẫu cũ còn sót trong FIFO (`adc_fifo_drain()`).
  2. Nạp cấu hình cho kênh DMA (`dma_channel_configure`) để truyền đúng `1024` mẫu từ `&adc_hw->fifo` vào `adcBuf` và kích hoạt kênh DMA.
  3. Bật ADC chạy tự do (`adc_run(true)`), chờ DMA thu đủ 1024 mẫu (`dma_channel_wait_for_finish_blocking`), sau đó tắt ngay ADC và xả FIFO. Nhờ toàn bộ 1024 mẫu được thu liên tiếp trong một đợt DMA phần cứng không có sự can thiệp của CPU, khoảng cách thời gian giữa hai mẫu liền kề $`\Delta t = 1/F_s`$ cố định tuyệt đối (zero jitter).
- **`buildAndSendFrame()`**:
  - Ghi 4 byte Header: `txFrame[0] = 0xAA`, `txFrame[1] = 0x55`, `txFrame[2] = rateCode`, `txFrame[3] = swByte`.
  - Khởi tạo `checksum = rateCode ^ swByte`.
  - Duyệt qua 1024 mẫu trong `adcBuf[i]`, mặt nạ lấy 12 bit thấp (`& 0x0FFF`), tách thành byte thấp `lo` và byte cao `hi` (định dạng Little-Endian), ghi vào `txFrame[4 + 2*i]` và cập nhật `checksum ^= lo; checksum ^= hi;`.
  - Ghi `txFrame[2052] = checksum` và đẩy nguyên khối `2053 byte` qua USB-CDC bằng một lời gọi `Serial.write(txFrame, 2053)` duy nhất.

---

## PHẦN 3: PHÂN TÍCH KIẾN TRÚC PHẦN MỀM WINFORMS C# (`BTL_UDMT`)

Ứng dụng máy tính `BTL_UDMT` được xây dựng trên nền tảng **.NET Framework 4.7.2 (Windows Forms)** gồm hai tệp chính:
- `Form1.Designer.cs` (`372` dòng): Khởi tạo bố cục giao diện 4 vùng bằng mã C# thuần (`BuildCustomUI()`), tông màu trắng – đen chuẩn báo cáo khoa học.
- `Form1.cs` (`1071` dòng): Xử lý truyền thông nối tiếp bất đồng bộ, giải mã khung nhị phân 2053 byte, đồng bộ công tắc phần cứng, khôi phục điện áp AFE, bắt điểm đồng bộ Trigger, đo 8 thông số tự động và vẽ đồ họa GDI+ hai lớp đệm (`DoubleBuffered = true`).

### 3.1. Kiến trúc đa luồng và Cơ chế Quản lý COM Bất đồng bộ Chống treo (`Form1.cs` dòng `149–402`)

Một lỗi phổ biến của các ứng dụng WinForms dùng `System.IO.Ports.SerialPort` là hiện tượng **đơ cứng toàn bộ cửa sổ giao diện (UI Freeze)** khi người dùng chọn nhầm cổng COM ảo (như cổng Bluetooth SPP) hoặc rút cáp đột ngột. `Form1.cs` giải quyết triệt để bài toán này bằng 4 lớp bảo vệ:

1. **Khởi tạo đối tượng `SerialPort` mới cho mỗi phiên kết nối (`CreateSerialPort`)**:
   - Đặt `ReadBufferSize = 65536` (64 KB, chứa được hơn 31 khung 2053 byte), `ReadTimeout = 200 ms`, `WriteTimeout = 200 ms`, `DtrEnable = true`, `RtsEnable = true` (bắt buộc để stack TinyUSB trên RP2040 nhận biết máy trạm đã mở cổng USB-CDC).
2. **Mở cổng bất đồng bộ có giới hạn thời gian `2000 ms` và bắt tay xác thực dữ liệu (`BtnConnect_Click`)**:
   - Tăng bộ đếm phiên `int mySeq = ++connectSeq;` để vô hiệu hóa mọi tác vụ nền của lần bấm trước.
   - Chạy `port.Open()` bên trong `Task.Run(...)` và chờ bằng `await Task.WhenAny(openTask, Task.Delay(2000))`. Nếu sau `2000 ms` driver Windows chưa mở xong cổng, ứng dụng lập tức hủy phiên, đóng cổng ngầm qua `ClosePortAsync(port)` và thông báo lỗi mà không làm đơ luồng giao diện.
   - Sau khi mở cổng thành công, ứng dụng gửi lệnh đồng bộ tốc độ `SendRateCommandToRP2040()` và cấu hình xung mẫu `SendGenCommandToRP2040()`, sau đó chờ tối đa `2000 ms` (vòng lặp `await Task.Delay(50)`) để kiểm tra cờ `gotValidFrame`. Nếu cổng COM đó không gửi về ít nhất 1 khung `0xAA 0x55` có mã kiểm tra XOR hợp lệ từ RP2040, ứng dụng tự động ngắt kết nối và báo *"Sai cổng COM"*.
3. **Ghi lệnh và Đóng cổng hoàn toàn trên luồng nền (`WriteCommandAsync`, `ClosePortAsync`)**:
   - Mọi lệnh `R<code>\n` và `G<freq>,<duty>\n` đều được gửi qua `Task.Run` dưới khóa `lock (writeLock)`.
   - Khi nhấn `Disconnect` hoặc bấm nút đóng cửa sổ `[X]` (`Form1_FormClosing`), cờ `isConnected = false` được hạ xuống **trước tiên**, giao diện chuyển trạng thái ngay tức khắc, còn thao tác `port.Close()` và `port.Dispose()` được đẩy sang luồng nền `Task.Run`, chặn đứng lỗi deadlock kinh điển giữa `SerialPort.Close()` và sự kiện `DataReceived`.

### 3.2. Bộ giải mã Khung Nhị phân 2053 Byte và Cơ chế Xả Bộ đệm Liên tục (`Form1.cs` dòng `417–515`)

- **Xả bộ đệm liên tục trong `SerialPort_DataReceived`**:
  - Mỗi lần sự kiện `DataReceived` kích hoạt trên luồng ThreadPool, hàm đọc tối đa `8` lượt × `8192 byte` từ driver cổng COM.
  - **Điểm mấu chốt chống treo khi dùng chế độ `Single`**: Ngay cả khi `singleCaptured == true` (đã bắt và giữ xong 1 khung sóng đơn), vòng lặp **vẫn tiếp tục gọi `port.Read(...)` để rút sạch dữ liệu khỏi bộ đệm hệ điều hành** rồi mới `continue` bỏ qua bước giải mã. Nhờ vậy, bộ đệm USB-CDC không bao giờ bị đầy và hàm `Serial.write()` trên RP2040 không bao giờ bị kẹt, nên khi người dùng chuyển từ `Single` về `Auto` hoặc `Normal`, dữ liệu mới luôn sẵn sàng tức thì.
- **Thuật toán đồng bộ khung tốc độ cao trong `ParseBinaryFrames()`**:
  - Giới hạn kích thước `rxBuffer` tối đa `4 × FRAME_SIZE` (`8212 byte`); nếu tồn đọng nhiều hơn do máy tính bận, tự động cắt bỏ các khung cũ để chỉ giữ `2` khung mới nhất (đảm bảo độ trễ hiển thị luôn thấp nhất).
  - Nếu 2 byte đầu không phải `0xAA, 0x55`, dùng `rxBuffer.IndexOf(0xAA, 1)` và `RemoveRange(0, nextSync)` để nhảy thẳng tới vị trí byte `0xAA` tiếp theo với độ phức tạp $`O(N)`$ (thay vì xóa từng byte gây tốn $`O(N^2)`$).
  - Kiểm tra mã `calcChecksum = rateCode ^ swByte ^ payload[0..2047]`. Khi khớp hoàn toàn với `rxBuffer[2052]`:
    - Giải mã 1024 mẫu 12-bit sang điện áp tại chân ADC:

      ```math
      \text{rawAdcVolts}[s] = \frac{(\text{hi} \ll 8) \mid \text{lo}}{4095.0} \times 3.301
      ```

    - Cập nhật `hwSwByte = swByte`, `hwRateCode = rateCode`, bật `hasNewFrame = true` và `gotValidFrame = true`.

### 3.3. Đồng bộ Công tắc Phần cứng & Khôi phục Điện áp Thực (`Form1.cs` dòng `520–654`)

- **Đồng bộ tự động nhưng vẫn cho phép thu phóng hiển thị `X2` (`SyncSwitchesFromHardware`)**:
  - Ở mỗi nhịp vẽ hình (`33 ms`), phần mềm tách `sw1 = sw & 0x03`, `sw2 = (sw >> 2) & 0x03`, `sw3 = (sw >> 4) & 0x01`:
    - Tự động chọn nút `DC / AC / GND` theo `sw1` và `1V / 0.1V / 10mV` theo `sw2`.
    - Đối với `sw3` (chỉ có 1 chân số `GPIO22` báo `1 = X1`, `0 = X5`): phần mềm lưu biến `lastHwSw3`. Chỉ khi người dùng thực sự gạt công tắc cơ khí `SW3` làm `sw3 != lastHwSw3`, giao diện mới tự nhảy nút Radio sang `X1` hoặc `X5`. Khi công tắc cơ khí đứng yên, người dùng có thể bấm chọn nút `X2` trên màn hình để phóng đại/thu nhỏ thang hiển thị (`GetVoltsPerDiv()`), trong khi phép tính điện áp thực trong `ProcessSignalAndMeasurements()` vẫn lấy đúng trọng số phần cứng `hwAlpha2` (`1.0` hoặc `0.2`) từ bit thứ 4 của `hwSwByte`!
- **Khôi phục điện áp thuận dấu và khử DC ở chế độ AC**:
  - Tính tổng độ lợi thực tế: `totalGain = AFE_AMP_GAIN * alpha1 * hwAlpha2`.
  - Quy đổi từng mẫu trên bộ đệm trung gian `workVolts`:

    ```math
    \text{workVolts}[i] = \frac{\text{rawAdcVolts}[i] - 1.650}{\text{totalGain}}
    ```

  - Tính giá trị trung bình một chiều `dcOffset = sum / 1024`. Nếu đang ở chế độ ghép `AC` (`radCplAC.Checked`), trừ tiếp `dcOffset` khỏi toàn bộ 1024 mẫu để đưa sóng xoay chiều cân bằng hoàn toàn quanh trục $`0\,\text{V}`$.

### 3.4. Bộ đồng bộ sóng Trigger, Kiến trúc Bộ đệm kép và Tính toán 8 Thông số đo (`Form1.cs` dòng `655–785`)

- **Thuật toán bắt điểm Trigger có ngưỡng trễ (`Hysteresis`)**:
  - Quét trong cửa sổ trung tâm từ mẫu `256` (`NUM_SAMPLES / 4`) đến mẫu `768` (`3 * NUM_SAMPLES / 4`) để luôn đảm bảo có đủ mẫu hiển thị cả phía trước (pre-trigger) và phía sau (post-trigger) điểm đồng bộ.
  - Độ rộng dải trễ chống nhiễu: $`\text{hyst} = 0.05 \times \text{VoltsPerDiv}`$.
  - Điều kiện bắt sườn lên (Rising Edge): $`\text{workVolts}[i-1] \lt (V_{\text{trig}} - \text{hyst})`$ **và** $`\text{workVolts}[i] \ge V_{\text{trig}}`$.
  - Điều kiện bắt sườn xuống (Falling Edge): $`\text{workVolts}[i-1] \gt (V_{\text{trig}} + \text{hyst})`$ **và** $`\text{workVolts}[i] \le V_{\text{trig}}`$.
- **Kiến trúc bộ đệm kép (`workVolts` → `inputSignalVolts`) giữ sóng ở chế độ `Normal` & `Single`**:
  - Mọi phép tính quy đổi và dò Trigger đều chạy trên mảng tạm `workVolts`.
  - Nếu đang ở chế độ `Normal` hoặc `Single` mà khung hiện tại không có sườn xung cắt qua ngưỡng Trigger (`!found`), hàm `return` ngay lập tức mà **không ghi đè** vào mảng `inputSignalVolts`. Nhờ đó, màn hình ở chế độ `Normal` luôn giữ nguyên dạng sóng hợp lệ cuối cùng thay vì bị nhấp nháy hoặc xóa trắng giữa các khung.
- **Thuật toán đo 8 đại lượng tự động (`Vmax, Vmin, Vpp, Vavg, Vrms, Freq, Period, Duty`)**:
  - Duyệt qua 1024 mẫu của `inputSignalVolts` để tìm cực đại $`V_{\text{max}}`$, cực tiểu $`V_{\text{min}}`$, biên độ đỉnh-đỉnh $`V_{pp} = V_{\text{max}} - V_{\text{min}}`$, giá trị trung bình $`V_{\text{avg}}`$ và giá trị hiệu dụng:

    ```math
    V_{\text{rms}} = \sqrt{\frac{1}{1024}\sum_{i=0}^{1023} V_{\text{in}}^2[i]}
    ```

  - Trong `CalculateFrequencyAndDuty(midLevel, vpp)`: Dùng bộ so sánh trễ hai ngưỡng quanh mức giữa $`\text{midLevel} = \frac{V_{\text{max}} + V_{\text{min}}}{2}`$ với dải trễ $`\text{hyst} = 0.10 \times V_{pp}`$ (chỉ bật cờ `armed = true` khi tín hiệu xuống dưới $`\text{midLevel} - \text{hyst}`$, và ghi nhận một chu kỳ mới khi tín hiệu vượt qua $`\text{midLevel} + \text{hyst}`$). Khi thu được $`K \ge 2`$ điểm cắt sườn lên tại các chỉ số mẫu $`n_0, n_1, \dots, n_{K-1}`$, số mẫu trung bình trên một chu kỳ và tần số được tính chính xác trên toàn chuỗi chu kỳ:

    ```math
    N_{\text{avg}} = \frac{n_{K-1} - n_0}{K - 1}, \quad f = \frac{F_s}{N_{\text{avg}}}, \quad T = \frac{1000}{f}\,(\text{ms}), \quad \text{Duty} = \frac{N_{\text{high}}}{1024} \times 100\%
    ```

### 3.5. Kết xuất Đồ họa GDI+ 2D và Tương tác Con trỏ (`Form1.cs` dòng `790–1070`)

- **Chu trình vẽ 5 lớp trong `PicScope_Paint`**:
  1. `DrawTopStatusBar`: Vẽ khung trạng thái (`CHƯA KẾT NỐI`, `ĐANG ĐO`, `TRIG'D`, `SINGLE (GIỮ)`), thông tin `[CPL] | Scale: V/div | Timebase: us/div | Trig: V` ở bên trái, và hiển thị trực tiếp **tốc độ lấy mẫu phần cứng thực tế `Fs: ... MSa/s` (hoặc `kSa/s`)** giải mã từ `hwRateCode` căn sát góc trên bên phải.
  2. `DrawGraticule`: Vẽ khung lưới $`10\,\text{ô ngang} \times 8\,\text{ô dọc}`$ với đường tâm chính chia vạch nhỏ $`1/5\,\text{ô}`$ ($`0.2\,\text{div}`$) chuẩn máy hiện sóng để bàn.
  3. `DrawMarkersAndTriggerLine`: Vẽ ký hiệu mốc `0V>` theo vị trí thanh trượt `trkVPos` và đường nét đứt mức kích hoạt `T>` theo `trkTrigLevel`.
  4. `DrawWaveform`: Tính số mẫu cần hiển thị trên 10 ô ngang:

     ```math
     N_{\text{screen}} = \text{clamp}\left(\lfloor 10 \times T_{\text{div}} \times F_s \rfloor,\, 20,\, 512\right)
     ```

     Căn điểm `triggerIndex` vào chính giữa màn hình cộng độ lệch ngang `trkHPos.Value`, ánh xạ từng mẫu sang tọa độ điểm ảnh $`(x, y)`$, vẽ hai đường gióng ngang $`V_{\text{max}} / V_{\text{min}}`$ và nối tuyến tính chuỗi điểm sóng bằng bút nét đen `1.6px` có khử răng cưa (`SmoothingMode.AntiAlias`).
  5. `DrawCursors`: Khi bật con trỏ đo, cho phép kéo chuột trái (đặt `Cursor 1`) và chuột phải (đặt `Cursor 2`) trực tiếp trên màn hình sóng để đo khoảng thời gian $`\Delta T\,(\text{ms})`$ và tần số tức thời $`1/\Delta T\,(\text{Hz})`$.

---

## PHẦN 4: TỔNG HỢP HÌNH ẢNH ĐÃ CHỤP VÀ TÍCH HỢP VÀO BÁO CÁO

Toàn bộ **13 hình minh họa** (6 sơ đồ lý thuyết, sơ đồ khối, sơ đồ nguyên lý chính thức, ảnh chụp hai mặt PCB và 7 hình chụp/kết xuất thực nghiệm trên giao diện WinForms `BTL_UDMT`) đã được hoàn thiện và chèn đồng bộ vào [Bao_Cao_OSCILLOSCOPE.docx](./Bao_Cao_OSCILLOSCOPE.docx).

| Ký hiệu hình | Vị trí trong báo cáo | Nội dung & Cấu hình thực nghiệm đã chèn |
| :--- | :--- | :--- |
| **`Hình 3.4`** | Chương 3 – Mục `3.4` | **Giao diện phần mềm máy hiện sóng WinForms `BTL_UDMT`**: Thể hiện đầy đủ khung lưới `10×8 div` nền trắng nét đen, bảng điều khiển bên phải với `12` thang `Time/Div` (`1 us/div` – `5 ms/div`), cụm `6. PHÁT XUNG VUÔNG TEST` (`1000 Hz`, `Duty 50%`) và tốc độ lấy mẫu `Fs: 100 kSa/s`. |
| **`Hình 4.1`** | Chương 4 – Mục `4.2` | **Kết quả đo mức nhiễu nền trên thang `10 mV/div` khi nối đất đầu đo**: Cấp nguồn xung `12V`, đặt `SW1 = DC`, `SW2 = 10mV`, `SW3 = X1`, nối đầu đo về `GND`; đường sóng phẳng quanh mốc `0V>` với nhiễu đỉnh-đỉnh chỉ khoảng `1 mVpp` (`Vpp` đo `0.00V`). |
| **`Hình 4.2`** | Chương 4 – Mục `4.3` | **So sánh đáp ứng sườn xung vuông khi thiếu bù và quá bù tụ tần số tại cầu chia `SW2`**: Cặp đồ thị WinForms ghép song song cho thấy rõ `(a) Thiếu bù` (hõm góc tại cạnh lên) và `(b) Quá bù` (gai vọt lố nhọn tại cạnh lên) trước khi chốt trị số tụ gốm chuẩn. |
| **`Hình 4.3`** | Chương 4 – Mục `4.3` | **Dạng sóng xung vuông `5 kHz` ở chế độ `DC` sau khi hoàn tất cân chỉnh tụ bù**: Đặt `SW1 = DC`, `SW2 = 1V`, `Time/Div = 50 us/div`, sóng vuông đối xứng, góc cạnh phẳng và không còn hõm hay vọt lố. |
| **`Hình 4.4`** | Chương 4 – Mục `4.3` | **Kết quả đo xung vuông chuẩn `1 kHz` tại thang thời gian `200 us/div`**: Hiển thị sắc nét với `Freq = 1.000 kHz`, `Period = 1.000 ms`, `Duty = 50.0%` và `Vpp = 3.12 V`. |
| **`Hình 4.5`** | Chương 4 – Mục `4.3` | **Dạng sóng xung vuông `200 kHz` phóng đại tại thang `1 us/div` ở tốc độ `3.00 MSa/s`**: Thể hiện rõ độ dốc cạnh lên thực tế `~0.55–1.0 µs` do giới hạn `GBWP = 3 MHz` của `TL084` và `Freq = 200.000 kHz`, `Vpp = 3.10 V`. |
| **`Hình 4.6`** | Chương 4 – Mục `4.4` | **Ảnh chụp màn hình thực tế WinForms `BTL_UDMT` đo xung `200 kHz` tại thang `5 us/div`**: Xác nhận đồng bộ công tắc `AC / 1V / X1`, trạng thái `TRIG'D`, tốc độ lấy mẫu `Fs: 3.00 MSa/s`, `Freq = 200.000 kHz`, `Period = 0.005 ms`, `Vrms = 1.55 V`. |
