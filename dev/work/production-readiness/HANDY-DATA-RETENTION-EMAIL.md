# Handy Hosting Data-Retention Question

**Status:** Draft only; not sent.  
**Suggested recipient:** `lars@sweettech.no` — listed as the contact in the official [Handy Hosting API v2 OpenAPI specification](https://www.handyfeeling.com/api/hosting/v2/docs/spec.yaml). The [Handy REST API v3 specification](https://www.handyfeeling.com/api/handy-rest/v3/docs/spec.yaml) separately lists `lars@ohdoki.com` as its API contact. If the Hosting API contact is not the right person, ask them to forward this to the privacy/data-retention contact.

**Subject:** Data retention for scripts uploaded through Handy Hosting API v2

Hello,

I maintain ScriptSyncPilot, a free, open-source Windows application that synchronizes a user's local funscript with a Handy device. The application uses Handy REST API v3/HSSP and the Handy Hosting API v2.

To set up HSSP, the application uploads a CSV file to `POST /api/hosting/v2/upload`. The CSV contains only the funscript action timestamps and positions. The Hosting API returns a temporary download URL, which the application passes to HSSP setup. The device then uses that URL to retrieve the script. The application does not upload the associated video.

The Hosting API documentation describes the returned URL as temporary, but I could not find a stated retention period for the uploaded file itself or a validity period for the URL. The Handy privacy policy says session data for online third-party synchronization is deleted within one hour after logoff and that a script stored on the device remains until overwritten. I could not determine whether those statements also cover the Hosting API upload and its generated download URL.

Could you please clarify:

1. **File retention:** How long is an uploaded CSV stored, including after it is no longer downloadable, and what event triggers its deletion?
2. **URL validity:** How long does the generated download URL remain usable, and can that period differ from the retention period of the underlying file?
3. **Session cleanup:** Does the privacy policy's one-hour session-data deletion statement apply to the uploaded file, the generated URL, or both? Does ending an HSSP session trigger either file deletion or URL invalidation?
4. **Logging and metadata:** What metadata is stored as part of the upload, and what operational or access logs are retained for this flow (for example, uploader IP address, upload time, script/download identifier, or download/access logs)? How long is each retained, including by any third-party hosting or delivery providers involved?
5. **Early deletion:** Is there a supported way for an application or user to invalidate the download URL or delete the uploaded file before its normal expiry?
6. **Application guidance:** Are there recommended privacy disclosures, attribution requirements, or other usage requirements that third-party application maintainers should follow when using these APIs?

We are documenting the application's data flows and want to describe Handy's handling of uploaded scripts and associated metadata accurately. I have not included any user connection key, script, or generated URL in this message.

Thank you,

Frank Yong  
ScriptSyncPilot maintainer

## References checked

- [Handy Hosting API v2 OpenAPI specification](https://www.handyfeeling.com/api/hosting/v2/docs/spec.yaml): calls uploads temporary and identifies the Hosting API contact above; no exact expiry period is specified.
- [Handy REST API v3 OpenAPI specification](https://www.handyfeeling.com/api/handy-rest/v3/docs/spec.yaml): lists `lars@ohdoki.com` as a separate API contact.
- [The Handy Privacy Policy](https://www.thehandy.com/privacy-policy/): describes data handling for device connectivity and online synchronization, including session data and script storage, but does not clearly specify retention of a separately uploaded Hosting API CSV.
