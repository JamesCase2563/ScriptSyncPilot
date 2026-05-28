# Online Security and the Handy

## Overview
The Handy requires a persistent Internet connection to synchronize with content and access features. Given the private nature of its use, understanding the security and privacy implications is important.

## Security Measures
- **End-to-end encryption**: All communication between the Handy and servers is encrypted via HTTPS.
- **Connection key system**: Each device uses a unique connection key, which can be changed at any time. Changing the key at the hardware level invalidates the old key.
- **No historic key records**: There are no records of previous keys, making tracking difficult for third parties.
- **Third-party access**: Third-party services can only identify your device by the connection key you provide. They do not receive your device's IP address.
- **Local configuration**: Local configuration is done via Bluetooth (approx. 10m range).

## Data Handling
- **Minimal data uploaded**: Only essential data is transmitted. The system does not upload your browser history, payment info, or other personal data.
- **No direct device access**: Third-party apps interact with the Handy via the API and do not have direct access to your device.

## Key Security Details
- **Key generation**: The connection key is generated using true random noise from the Wi-Fi antenna.
- **Key length**: Variable from 5 to 32 characters, using lower case, upper case, and numbers (62 possible characters per position).
- **Brute force resistance**: Even the shortest key (5 chars) has over 916 million combinations; the longest (32 chars) has over 2.2e57 combinations.
- **Key compromise**: If a key is brute-forced and stolen, the attacker can only control the device while it is online. Changing the key immediately revokes access.

## Device Operation
- **Offline by default**: The Handy is offline by default. Wi-Fi must be manually activated each time the device is powered on.
- **Firmware and API**: Online connectivity allows for firmware updates and API access. If you wish to revoke third-party access, simply change your connection key.

## Privacy Philosophy
- **Privacy by default**: The Handy is designed with privacy as a priority. There is little incentive to collect user data, reducing future data collection risks.

## CTO Statement
> “If it's online, it can be hacked or manipulated. ... Generating a new key renders the old one useless. ... Our Handy-server connection system is fire and forget. ... Privacy by default is very important for us.”

## Additional Links
- [thehandy.com](https://thehandy.com/)
- [Developer Docs](https://intercom.help/ohdoki/en/collections/5757680-developer)

---
*Extracted and summarized from [Ohdoki Help Center](https://intercom.help/ohdoki/en/articles/9034256-online-security-and-the-handy) on May 28, 2026.*
