# Glossary

| Term | Meaning |
| --- | --- |
| Account | A demo balance holder identified by a GUID. The treasury is an internal system account. |
| Amount minor | A positive integer amount in the currency's minor unit; for BRL, 100 means R$ 1.00. |
| Deposit | A simulated posting from the treasury to a regular account. |
| Transfer | A simulated posting between two different regular accounts. |
| Payment | The recorded deposit or transfer returned by the API. |
| Idempotency key | A case-sensitive client string identifying one payment request. Repeating an identical request returns the original payment. |
| Problem code | The stable `code` property in an `application/problem+json` error response. |
