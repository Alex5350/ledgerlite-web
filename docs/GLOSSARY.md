# Glossary

Terms used across LedgerLite Web's documentation: plain English first, precisely second. The
bookkeeping terms are shared with the LedgerLite API; their fuller reference lives in the
[API repository's glossary](https://github.com/Alex5350/ledgerlite/blob/main/docs/GLOSSARY.md).

## Bookkeeping terms

| Term | In plain English | Precisely |
|---|---|---|
| Double-entry bookkeeping | The discipline of recording every transaction twice, once as money leaving one pot and once as money arriving in another, so the books always add up and nothing can appear or vanish unexplained. | The accounting method this app is built on: every transaction is a journal entry whose debit and credit totals must be equal; the invariant is enforced in the API's domain model, never by the UI alone. |
| Journal entry | The record of one transaction: what it was for, and which accounts it touched on each side. | A set of at least two entry lines, each with exactly one non-zero side (debit or credit), posted into one fiscal period; total debits must equal total credits before the API accepts it. |
| Debit / credit | The two columns of bookkeeping: the "left side" and "right side" of a transaction. Neither is good or bad; they just have to balance. | The two sides of an entry line; each line carries exactly one non-zero amount on one side, and the entry is valid only when the sum of debits equals the sum of credits. |
| Balanced entry | An entry whose two sides add up to the same amount. In this app, the Post button unlocks only when the running totals match. | Debit total equals credit total; the journal editor shows a Balanced / Out of balance indicator and gates submit on the condition, and the API independently re-validates it server-side. |
| Chart of accounts | The list of pots you keep: checking, rent, groceries, and so on. It is the app's Accounts page. | The accounts belonging to one fiscal period, each with a number unique within that period and a type (asset, liability, equity, revenue, expense) that drives its badge tone in the UI. |
| Trial balance | The classic health check: list every account's debits and credits, total both columns, and confirm the columns match. If they match, the books balance. | A per-period report of each account's debit and credit totals with grand totals and a Balanced indicator, computed by the API and rendered by the client. |
| Fiscal period close | Closing the books on a month or year once you are done with it. After closing, that period's numbers are final: no more entries can be posted into it. | The lifecycle transition that makes a period immutable; the API rejects posting into (or double-closing) a closed period, and the UI warns explicitly before closing. |
| Budget vs actual | A plan next to reality: how much you meant to spend in a category versus how much you actually did. | A spending limit set on an expense account for a period, evaluated against posted entries; the UI turns the progress bar amber at 80% and red at 100% of the limit. |

## Interface and engineering terms

| Term | In plain English | Precisely |
|---|---|---|
| Blazor | The part of .NET that builds interactive web pages in C#: you write components, and it takes care of updating the page when data changes. | The web UI framework used for this client; components live in `LedgerLite.Web.Client` and run in two hosting models. |
| Server-side vs WebAssembly rendering | Two ways to run the same page: the server does the work and streams changes to you, or your browser runs the app itself. Each trades startup speed against not needing the server for every click. | Interactive Server runs components in the ASP.NET host over a SignalR circuit; WebAssembly downloads the component library and runs it in the browser, calling the API directly (why CORS for the UI origin matters). |
| Interactive Auto | Start one way, settle the other: the first visit is served fast by the server, later visits run in your browser. You get a quick start and a snappy app without downloading everything up front. | The Blazor render mode where pages render as static SSR, go interactive over SignalR first, and switch to WebAssembly on subsequent visits; one component library serves both hosts. |
| Prerender | Drawing the page before it is fully awake, so you see content immediately instead of a blank screen while the interactive parts load. | Static server-side rendering of the initial HTML during first load; code here must tolerate it (the token store is prerender-safe). |
| Circuit | The live connection between your browser tab and the server while a page is interactive. Each tab's circuit is its own; what happens in one must not affect another. | The per-user Blazor Server connection scope; the HTTP handler chain to the API is scoped per circuit so one session's disposal cannot break another's. |
| Live validation | The form checks your work as you type and shows the verdict on the spot, rather than waiting for you to submit and be scolded. | Client-side checking in the component (running debit/credit totals gating the Post button); always paired with server-side re-validation in the API, never a substitute for it. |
| Design system | A shared visual and behavioral vocabulary: the same kind of button, badge or bar means and behaves the same thing on every page, because it is the same component. | The hand-built UI kit over Tailwind CSS v4 `@theme` tokens: `BudgetBar` owns the 80%/100% threshold tones, `Field` owns validation display, `DataTable<T>` owns tabular layout; no third-party component kit. |
