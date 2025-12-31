# acs
Automotive control systems. Development and demonstration board. Generic usage. Focused on designing and developing a CAN bus network for intrusion detection system studies.

# What does this repository contain?
This repo contains a custom board design to simulate automotive ECUs like VCU, BMS, Inverter etc. Moreover, this repo also include a CAN bus database structure to create a real world simulation on a custom test bench. This test bench is designed with 4 main ECU. It can be extended or collapsed based on the research study. Additionally, following type of datasets are shared openly in csv format.
 - Attack-free
 - Periodic message injection (Database IDs)
 - Periodic message injection (Not defined in database IDs)
 - DoS
 - Fuzzy

# What does it look like as a single hardware?
![The main PCB of the ACS](https://raw.githubusercontent.com/enginsubasi/acs/refs/heads/main/dc/im/acs-pcba.png)

# Main feature list
 - 12-24V Power supply
 - USB Connection
 - RS-485 interface
 - CAN Bus interface
 - 3 Channel GPIO
 - 6 Channel PWM (3 normal, 3 complementary)
 - 4 Channel PWM
 - 6 Channel ADC
 - UEXT (I2C excluded. It can be used by UART pins. This is not a standard useage)
 - CPU, ALARM and ERROR LEDs
 - SWD Programming interface
 - CPU clock speed is up to 48 MHz for STM32F0/72 MHz for STM32F1 series microcontrollers
 - 128 kB Flash is used for applications
 

