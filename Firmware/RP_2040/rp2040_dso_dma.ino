#include <Arduino.h>
#include <hardware/adc.h>
#include <hardware/clocks.h>
#include <hardware/dma.h>
#include <hardware/pwm.h>
#include <hardware/vreg.h>

#define PIN_ADC_IN          26
#define ADC_CHANNEL         0
#define PIN_ADC_VOL         27
#define PIN_ADC_MODE        28
#define PIN_SW3             22
#define PIN_TEST_PWM        0
#define PIN_TRIG_PWM        20

#define NUM_SAMPLES         1024
#define FRAME_HEADER_SIZE   4
#define FRAME_SIZE          (FRAME_HEADER_SIZE + (NUM_SAMPLES * 2) + 1)
#define SYNC_BYTE_1         0xAA
#define SYNC_BYTE_2         0x55
#define FRAME_INTERVAL_MS   33

const float RATE_CLKDIV[9] = { 0.0f, 191.0f, 479.0f, 959.0f, 2399.0f, 4799.0f, 0.0f, 0.0f, 0.0f };

uint8_t currentRateCode = 2;
uint32_t genFreqHz = 1000;
uint8_t genDutyPct = 50;
bool pllBoosted = false;
bool adcBoosted = false;

uint16_t adcBuf[NUM_SAMPLES];
uint8_t txFrame[FRAME_SIZE];

int dmaChan = -1;
dma_channel_config dmaCfg;
unsigned long lastFrameMs = 0;

void initPwmPin(uint pin, float clkDiv, uint16_t wrap, uint16_t level) {
  gpio_set_function(pin, GPIO_FUNC_PWM);
  uint slice = pwm_gpio_to_slice_num(pin);
  pwm_config cfg = pwm_get_default_config();
  pwm_config_set_clkdiv(&cfg, clkDiv);
  pwm_config_set_wrap(&cfg, wrap);
  pwm_init(slice, &cfg, true);
  pwm_set_gpio_level(pin, level);
}

void applyGenPwm(uint32_t freqHz, uint8_t dutyPct) {
  uint32_t sysHz = clock_get_hz(clk_sys);
  uint32_t minHz = (sysHz / 16000000UL) + 1UL;
  if (freqHz < minHz) freqHz = minHz;
  if (freqHz > 500000UL) freqHz = 500000UL;
  if (dutyPct < 1) dutyPct = 1;
  if (dutyPct > 99) dutyPct = 99;

  genFreqHz = freqHz;
  genDutyPct = dutyPct;

  double totalTicks = (double)sysHz / (double)freqHz;
  double div = totalTicks / 65536.0;
  if (div < 1.0) div = 1.0;
  if (div > 255.9) div = 255.9;

  uint32_t wrap = (uint32_t)(totalTicks / div + 0.5);
  if (wrap > 65536UL) wrap = 65536UL;
  if (wrap < 2UL) wrap = 2UL;
  uint16_t wrapVal = (uint16_t)(wrap - 1UL);
  uint16_t level = (uint16_t)((wrap * (uint32_t)dutyPct) / 100UL);
  initPwmPin(PIN_TEST_PWM, (float)div, wrapVal, level);
}

void setupPwmOutputs() {
  uint32_t sysHz = clock_get_hz(clk_sys);
  uint32_t trigWrap = (sysHz / 100000UL) - 1UL;
  initPwmPin(PIN_TRIG_PWM, 1.0f, (uint16_t)trigWrap, (uint16_t)((trigWrap + 1UL) / 2UL));
  applyGenPwm(genFreqHz, genDutyPct);
}

void ensureBoostPll() {
  if (pllBoosted) return;
  clock_configure(clk_adc, 0, CLOCKS_CLK_ADC_CTRL_AUXSRC_VALUE_CLKSRC_PLL_USB, 48000000, 48000000);
  vreg_set_voltage(VREG_VOLTAGE_1_20);
  delay(5);
  set_sys_clock_khz(288000, true);
  pllBoosted = true;
  setupPwmOutputs();
}

void applySampleRate(uint8_t code) {
  if (code > 8) code = 8;
  currentRateCode = code;

  if (code <= 5) {
    if (adcBoosted) {
      adc_run(false);
      clock_configure(clk_adc, 0, CLOCKS_CLK_ADC_CTRL_AUXSRC_VALUE_CLKSRC_PLL_USB, 48000000, 48000000);
      adcBoosted = false;
    }
    adc_set_clkdiv(RATE_CLKDIV[code]);
    return;
  }

  ensureBoostPll();
  uint32_t targetAdcHz = (code == 6) ? 96000000UL : ((code == 7) ? 144000000UL : 288000000UL);
  adc_run(false);
  clock_configure(clk_adc, 0, CLOCKS_CLK_ADC_CTRL_AUXSRC_VALUE_CLKSRC_PLL_SYS, 288000000, targetAdcHz);
  adcBoosted = true;
  adc_set_clkdiv(0.0f);
}

void setupAdcAndDma() {
  pinMode(PIN_SW3, INPUT);
  adc_init();
  adc_gpio_init(PIN_ADC_IN);
  adc_gpio_init(PIN_ADC_VOL);
  adc_gpio_init(PIN_ADC_MODE);
  adc_select_input(ADC_CHANNEL);
  adc_fifo_setup(true, true, 1, false, false);
  applySampleRate(currentRateCode);

  dmaChan = dma_claim_unused_channel(true);
  dmaCfg = dma_channel_get_default_config(dmaChan);
  channel_config_set_transfer_data_size(&dmaCfg, DMA_SIZE_16);
  channel_config_set_read_increment(&dmaCfg, false);
  channel_config_set_write_increment(&dmaCfg, true);
  channel_config_set_dreq(&dmaCfg, DREQ_ADC);
}

uint8_t read3LevelChannel(uint ch, uint8_t prev) {
  adc_select_input(ch);
  (void)adc_read();
  uint16_t s[5];
  for (int i = 0; i < 5; i++) {
    s[i] = adc_read();
  }
  for (int i = 0; i < 4; i++) {
    for (int j = i + 1; j < 5; j++) {
      if (s[j] < s[i]) {
        uint16_t t = s[i];
        s[i] = s[j];
        s[j] = t;
      }
    }
  }
  float v = s[2] * (3.301f / 4095.0f);
  if (v <= 0.5f) return 0;
  if (v >= 1.5f && v <= 1.8f) return 1;
  if (v >= 2.7f) return 2;
  return prev;
}

uint8_t readSwitchByte() {
  static unsigned long lastSwMs = 0;
  static bool swInit = false;
  static uint8_t sw1 = 2;
  static uint8_t sw2 = 0;

  unsigned long now = millis();
  if (!swInit || (now - lastSwMs) >= 60) {
    bool wasBoost = adcBoosted;
    if (wasBoost) {
      adc_run(false);
      clock_configure(clk_adc, 0, CLOCKS_CLK_ADC_CTRL_AUXSRC_VALUE_CLKSRC_PLL_USB, 48000000, 48000000);
      adc_set_clkdiv(0.0f);
      adcBoosted = false;
    }
    sw1 = read3LevelChannel(2, sw1);
    sw2 = read3LevelChannel(1, sw2);
    adc_select_input(ADC_CHANNEL);
    if (wasBoost) {
      applySampleRate(currentRateCode);
    }
    lastSwMs = now;
    swInit = true;
  }

  uint8_t sw3 = digitalRead(PIN_SW3) ? 1 : 0;
  return (sw1 & 0x03) | ((sw2 & 0x03) << 2) | ((sw3 & 0x01) << 4);
}

void readHostCommand() {
  static char cmdBuf[32];
  static uint8_t cmdLen = 0;

  while (Serial.available() > 0) {
    int ch = Serial.read();
    if (ch == '\r') continue;
    if (ch == '\n') {
      if (cmdLen > 0) {
        cmdBuf[cmdLen] = '\0';
        if (cmdBuf[0] == 'R') {
          applySampleRate((uint8_t)atoi(cmdBuf + 1));
        } else if (cmdBuf[0] == 'G') {
          char* comma = strchr(cmdBuf + 1, ',');
          if (comma != nullptr) {
            *comma = '\0';
            uint32_t f = (uint32_t)atol(cmdBuf + 1);
            uint8_t d = (uint8_t)atoi(comma + 1);
            applyGenPwm(f, d);
          }
        }
        cmdLen = 0;
      }
    } else if (cmdLen < (sizeof(cmdBuf) - 1)) {
      cmdBuf[cmdLen++] = (char)ch;
    }
  }
}

void captureBlockDMA() {
  adc_run(false);
  adc_fifo_drain();

  dma_channel_configure(dmaChan, &dmaCfg, adcBuf, &adc_hw->fifo, NUM_SAMPLES, true);

  adc_run(true);
  dma_channel_wait_for_finish_blocking(dmaChan);
  adc_run(false);
  adc_fifo_drain();
}

void buildAndSendFrame(uint8_t rateCode, uint8_t swByte) {
  txFrame[0] = SYNC_BYTE_1;
  txFrame[1] = SYNC_BYTE_2;
  txFrame[2] = rateCode;
  txFrame[3] = swByte;

  uint8_t checksum = (uint8_t)(rateCode ^ swByte);
  for (int i = 0; i < NUM_SAMPLES; i++) {
    uint16_t raw = adcBuf[i] & 0x0FFF;
    uint8_t lo = (uint8_t)(raw & 0xFF);
    uint8_t hi = (uint8_t)((raw >> 8) & 0xFF);

    int pos = FRAME_HEADER_SIZE + (i * 2);
    txFrame[pos]     = lo;
    txFrame[pos + 1] = hi;
    checksum ^= lo;
    checksum ^= hi;
  }

  txFrame[FRAME_SIZE - 1] = checksum;
  if (Serial) {
    Serial.write(txFrame, FRAME_SIZE);
  }
}

void setup() {
  Serial.begin(115200);
  setupPwmOutputs();
  setupAdcAndDma();
}

void loop() {
  readHostCommand();

  if (!Serial) {
    delay(10);
    return;
  }

  unsigned long now = millis();
  if ((now - lastFrameMs) < FRAME_INTERVAL_MS) {
    return;
  }
  lastFrameMs = now;

  uint8_t swByte = readSwitchByte();
  uint8_t usedRate = currentRateCode;
  captureBlockDMA();
  buildAndSendFrame(usedRate, swByte);
}
