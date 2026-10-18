# Handy Hosting Data-Retention Question

**Status:** Draft only; not sent.  
**Suggested recipient:** `lars@sweettech.no` — listed as the contact in the official [Handy Hosting API v2 OpenAPI specification](https://www.handyfeeling.com/api/hosting/v2/docs/spec.yaml). The [Handy REST API v3 specification](https://www.handyfeeling.com/api/handy-rest/v3/docs/spec.yaml) separately lists `lars@ohdoki.com` as its API contact. If the Hosting API contact is not the right person, ask them to forward this to the privacy/data-retention contact.

**Subject:** Data retention for scripts uploaded through Handy Hosting API v2

Hello,

I maintain ScriptSyncPilot, a free, open-source Windows application that synchronizes a user's local funscript with a Handy device. The application uses Handy REST API v3/HSSP and the Handy Hosting API v2.

To set up HSSP, the application uploads a CSV file to `POST /api/hosting/v2/upload`. The CSV contains only the funscript action timestamps and positions. The Hosting API returns a temporary download URL, which the application passes to HSSP setup. The device then uses that URL to retrieve the script. The application does not upload the associated video.

The Hosting API documentation describes the returned URL as temporary, but I could not find the upload's retention period. The Handy privacy policy says session data for online third-party synchronization is deleted within one hour after logoff and that a script stored on the device remains until overwritten. I could not determine whether those statements cover the uploaded CSV itself.

Could you please clarify:

1. How long is an uploaded CSV retained and downloadable, and what event causes it to expire or be deleted?
2. Is the uploaded file deleted when the associated HSSP session ends, or does it have a separate retention period?
3. Does the privacy policy's one-hour session-data deletion statement apply to the Hosting API upload and its generated download URL?
4. What request or usage data does Handy retain for this flow (for example, uploader IP address, upload time, script/download identifier, or download/access logs), and for how long?
5. Is there a supported way for an application or user to invalidate/delete an uploaded script before its normal expiry?
6. Is there a specific privacy notice, attribution, or other guidance you recommend third-party application maintainers provide when using these APIs?

We are documenting the application's data flows and want to describe Handy's handling accurately. I have not included any user connection key, script, or generated URL in this message.

Thank you,

Frank Yong  
ScriptSyncPilot maintainer

## References checked

- [Handy Hosting API v2 OpenAPI specification](https://www.handyfeeling.com/api/hosting/v2/docs/spec.yaml): calls uploads temporary and identifies the Hosting API contact above; no exact expiry period is specified.
- [Handy REST API v3 OpenAPI specification](https://www.handyfeeling.com/api/handy-rest/v3/docs/spec.yaml): lists `lars@ohdoki.com` as a separate API contact.
- [The Handy Privacy Policy](https://www.thehandy.com/privacy-policy/): describes data handling for device connectivity and online synchronization, including session data and script storage, but does not clearly specify retention of a separately uploaded Hosting API CSV.
