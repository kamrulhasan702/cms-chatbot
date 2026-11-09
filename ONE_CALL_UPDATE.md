# One-call text pipeline (2026-11-07)

For ordinary text questions: local query normalization + local manual search -> **one** chat-completion call -> local evidence-ID and UI-label checks -> answer and mapped figures. Known rich-text image-insertion questions are answered deterministically without any Groq call. No Groq embeddings or AI planning/review calls are used in the request path.

Screenshots require **one vision request plus one answer request** (up to two Groq calls). Unsupported questions with no local evidence use zero Groq calls. Retries by a proxy/client or other unrelated network requests are outside this per-request count.

The local validator is conservative but cannot guarantee that every unquoted sentence is entailed by the manual. Cross-language search is dictionary-based and may refuse some valid questions, particularly ambiguous follow-ups. Do not claim zero hallucinations or perfect Banglish retrieval.

The old test harness contains some tests for the retired AI planning/review workflow and should be updated before relying on the full test suite. The `dotnet` SDK is unavailable in this packaging environment, so compile/runtime verification is pending on the user's PC.

## Run on Windows PowerShell

Preserve your existing `.env` privately. Extract the ZIP, open PowerShell inside the extracted folder, then:

```powershell
docker build -t cms-chatbot:onecall .
docker rm -f cms-chatbot-test
docker run -d --name cms-chatbot-test -p 8090:8080 --env-file .env cms-chatbot:onecall
docker logs cms-chatbot-test --tail 50
```

Never commit `.env` or your API key. Rotate any key previously exposed.
