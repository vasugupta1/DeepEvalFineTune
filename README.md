This project is a project which is used to discover and improve on how deep eval is done in industry to better understand how LLM outputs can be judged via difference tests and methodolies. With the eval results we can finetune models to get better output from our models going forward.

Product Domain
In order to simulate a real world business product, we are going to use SCA (Strong Customer Auth), this protocol generarlly is complex and is hidden behind a black box, leaving engineering teams, product owners , business anaylystss and customers confused on which part of the journey has failed which untimaltiuy fails a transcation.

Some of the key questions that are normally asked
1. Why did my payment just get declined at checkout?
2. Why did it ask for extra factors to authenticate with when its a low value transcation
3. I didn't get any factors to authenticate with.


The system:
- Inspects real-time SCA telemetry, 3DS2 session logs, and cumulative exemption counters (such as contactless tap totals and low-value transaction limits).
- Cross-references SCA exemption rules and 3DS error code dictionaries.
- Translates cryptic payment gateway responses into plain-English customer explanations—while strictly withholding internal fraud-engine risk scores.
- Pauses at a checkpoint for verification or agent approval before executing account mutations (like clearing a 3DS soft-lock or re-binding a device).

The sample product is the vehicle; the focus is on **eval-driven development**: establishing a baseline with DeepEval early, measuring the lift of every architectural change (RAG, reranking, guardrails), and closing the loop with an automated **LangGraph fine-tuning & teacher-distillation workflow**.



## 1. System Topology & Tech Stack

This architecture mirrors production fintech microservices so that evals and fine-tuning are exercised against realistic components (routing, tool calls, hybrid retrieval, structured outputs, and human-in-the-loop checkpoints).

| Layer | Technology | Role in the Project |
|---|---|---|
| **Core API & State Orchestrator** | Python 3.13 + FastAPI + LangGraph | Runs the stateful chatbot workflow, manages checkpoints in Postgres, and exposes streaming SSE & REST endpoints. |
| **Downstream Domain Services** | CSharp Mock API | Simulates the SCA Telemetry Service (3DS2 logs), Exemption Counter Service (cumulative tap/spend limits), and Device Binding Registry. |
| **AI Gateway & Routing** | LiteLLM Proxy | Unifies local models (Ollama / vLLM / ROCm) and frontier APIs with token budgets, semantic caching (Redis), and automatic fallback chains. |
| **Knowledge & Retrieval Store** | PostgreSQL (`pgvector` + `tsvector`) | Stores SCA exemption rules, EMV 3DS2 reason code dictionaries, and customer device troubleshooting guides for Hybrid Search + Reciprocal Rank Fusion (RRF). |
| **Guardrails & Privacy** | Microsoft Presidio + Pydantic | Redacts customer PII (PANs, phone numbers, device IDs, OTPs) before prompts leave the boundary; enforces strict schema validation on all LLM outputs. |
| **Observability & Dataset Harvesting** | Langfuse (self-hosted via Docker) | Captures OpenTelemetry traces of every graph node, prompt version, token cost, and latency percentile; exports node-level spans for fine-tuning. |
| **Automated CI/CD Evals** | DeepEval + `Synthesizer` + pytest | Generates synthetic golden datasets and tests retrieval precision/recall, RAG faithfulness, tool correctness, and custom security/clarity rubrics (`GEval`). |
| **Eval-Driven Fine-Tuning Loop** | LangGraph + LangChain + DeepEval + TRL/PEFT (LoRA) or OpenAI Fine-Tuning API | Orchestrates a cyclical evaluate → diagnose failure mode → synthesize & mix replay dataset → fine-tune / distill → re-evaluate workflow with human promotion gates. |

```mermaid
flowchart LR
    Client["Customer Chat / App"] --> API["FastAPI + LangGraph"]
    API --> Presidio["Presidio PII Redaction"]
    API --> LiteLLM["LiteLLM Proxy"]
    LiteLLM --> Local["Local / Fine-Tuned Models (Ollama / vLLM)"]
    LiteLLM --> Frontier["Frontier Teacher APIs"]
    LiteLLM --> Redis["Redis Semantic Cache"]
    API --> PG["PostgreSQL (pgvector + tsvector + PostgresSaver)"]
    API --> Domain["SCA Tools (3DS Logs / Counters / Device State)"]
    API --> Langfuse["Langfuse (OTel Traces & Span Export)"]
    LiteLLM --> Langfuse
    FT["LangGraph Fine-Tuning & DeepEval Loop"] --> API
    FT --> Langfuse
    FT --> LiteLLM