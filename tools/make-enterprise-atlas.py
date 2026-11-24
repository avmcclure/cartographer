#!/usr/bin/env python3
"""Writes a large, fictional atlas shaped like a Fortune 500 estate, for trying the CLI and the site at scale.

  python3 tools/make-enterprise-atlas.py <dir>          # then: cartographer check --atlas <dir>

Kestrel Group is made up: a retailer with stores, e-commerce, its own logistics and a consumer finance arm, part way
through moving from Azure DevOps to GitHub. Every name, person and number here is invented. The output is the same on
every run (seeded), so the map it draws can be compared between builds.
"""
import os, random, re, sys, textwrap

random.seed(500)
OUT = sys.argv[1] if len(sys.argv) > 1 else "enterprise-atlas"
SCALE = 2   # repositories per domain are multiplied by this
ORG = "kestrel"
TODAY = "2026-11-20"

# Domains: (key, name, azdo project, team, how many repos, databases, legacy share)
DOMAINS = [
    ("customer", "Customer", "Customer", "Customer Platform team", 22, ["Customer", "CustomerConsent", "Preferences"], 0.1),
    ("ecom", "E-commerce", "Digital", "Digital Commerce team", 34, ["Basket", "WebContent", "Checkout"], 0.05),
    ("stores", "Stores", "Stores", "Store Systems team", 30, ["StoreOps", "Tills"], 0.45),
    ("orders", "Orders", "Orders", "Order Management team", 26, ["Orders", "Returns"], 0.15),
    ("pricing", "Pricing and promotions", "Pricing", "Pricing team", 18, ["Pricing", "Promotions"], 0.2),
    ("catalog", "Product catalogue", "Product", "Product Data team", 20, ["Catalogue", "Media"], 0.1),
    ("inventory", "Inventory", "SupplyChain", "Stock team", 24, ["Inventory", "Replenishment"], 0.3),
    ("warehouse", "Warehouse", "SupplyChain", "Warehouse Systems team", 28, ["WMS", "Labour"], 0.5),
    ("transport", "Transport", "Logistics", "Transport team", 22, ["Fleet", "Routing"], 0.35),
    ("vendors", "Suppliers", "Merchandising", "Supplier Systems team", 16, ["Suppliers", "PurchaseOrders"], 0.4),
    ("finance", "Finance", "Finance", "Finance Systems team", 30, ["Ledger", "Payables", "Receivables"], 0.55),
    ("payroll", "People and payroll", "People", "People Systems team", 18, ["HR", "Payroll", "Rota"], 0.5),
    ("loyalty", "Loyalty", "Customer", "Loyalty team", 16, ["Loyalty"], 0.1),
    ("credit", "Kestrel Money", "Money", "Consumer Finance team", 24, ["Accounts", "Lending", "Collections"], 0.3),
    ("service", "Customer service", "Service", "Contact Centre Systems team", 16, ["Cases"], 0.25),
    ("marketing", "Marketing", "Marketing", "Marketing Technology team", 18, ["Campaigns", "Audiences"], 0.1),
    ("data", "Data platform", "Data", "Data Platform team", 26, ["Warehouse", "Lake"], 0.05),
    ("identity", "Identity", "Platform", "Identity team", 10, ["Identity"], 0.1),
    ("platform", "Platform", "Platform", "Platform Engineering team", 24, [], 0.05),
]

NOUNS = {
    "customer": ["profile", "address", "consent", "contact", "household", "merge", "gdpr", "segment", "preference", "identity-link"],
    "ecom": ["basket", "checkout", "search", "browse", "wishlist", "content", "sitemap", "reviews", "recommendations", "delivery-slot", "gift-card"],
    "stores": ["till", "store-hub", "shelf-edge", "click-collect", "store-stock", "cash-office", "price-label", "store-rota", "kiosk", "scale"],
    "orders": ["order", "returns", "refund", "fulfilment", "split", "cancellation", "order-status", "invoice", "dispatch-note"],
    "pricing": ["price", "promotion", "markdown", "coupon", "price-file", "competitor-price", "margin"],
    "catalog": ["product", "range", "attribute", "media", "category", "barcode", "supplier-item", "nutrition", "pim-sync"],
    "inventory": ["stock", "replenishment", "forecast", "allocation", "transfer", "stock-count", "safety-stock", "write-off"],
    "warehouse": ["receiving", "putaway", "picking", "packing", "despatch", "slotting", "labour", "yard", "cycle-count", "label"],
    "transport": ["route", "fleet", "tracking", "carrier", "proof-of-delivery", "telematics", "load-plan", "driver"],
    "vendors": ["supplier", "purchase-order", "asn", "edi", "supplier-invoice", "rebate", "onboarding"],
    "finance": ["ledger", "payables", "receivables", "bank-rec", "vat", "month-end", "cost-centre", "fixed-assets", "treasury", "expenses"],
    "payroll": ["payroll", "employee", "rota", "timesheet", "absence", "pension", "recruitment", "starter-leaver"],
    "loyalty": ["points", "member", "offer", "voucher", "tier", "statement"],
    "credit": ["account", "application", "credit-check", "statement", "collections", "card", "payment-plan", "fraud", "affordability"],
    "service": ["case", "chat", "telephony", "complaint", "knowledge", "callback"],
    "marketing": ["campaign", "audience", "email", "sms", "push", "attribution", "survey"],
    "data": ["ingest", "warehouse-load", "lake-sync", "sales-cube", "stock-cube", "dq-check", "feature-store", "report-pack", "kpi"],
    "identity": ["sign-in", "token", "directory-sync", "mfa", "access-review"],
    "platform": ["common", "logging", "config", "messaging", "http-client", "feature-flags", "audit", "secrets", "scheduler", "health"],
}

MODERN = [("service", 0.40), ("job", 0.18), ("site", 0.12), ("etl", 0.08), ("report", 0.06), ("library", 0.16)]
LEGACY = [("desktop-app", 0.30), ("job", 0.25), ("site", 0.15), ("etl", 0.15), ("report", 0.15)]
STACKS = {"modern": ["csharp", "net8.0"], "older": ["csharp", "net48"], "vb6": ["vb6"], "asp": ["classic-asp", "vbscript"], "sql": ["tsql"]}

EXTERNALS = [
    ("card-acquirer", "Card acquirer", "Authorises and settles card payments taken in stores and online."),
    ("open-banking", "Open banking provider", "Takes bank-to-bank payments and reads bank statements."),
    ("tax-engine", "Tax engine", "Works out VAT on baskets and invoices."),
    ("credit-bureau", "Credit bureau", "Credit checks for Kestrel Money applications."),
    ("fraud-screening", "Fraud screening service", "Scores orders and applications for fraud."),
    ("parcel-carrier-a", "Parcel carrier A", "Delivers home orders under a national contract."),
    ("parcel-carrier-b", "Parcel carrier B", "Next-day and named-day parcel delivery."),
    ("pallet-network", "Pallet network", "Moves pallets between depots and large suppliers."),
    ("bank-payments", "Corporate bank", "Pays suppliers and staff, and returns statements."),
    ("payroll-bureau", "Payroll bureau", "Runs the statutory payroll calculation and pension feeds."),
    ("tax-authority", "Tax authority gateway", "Receives VAT and payroll submissions."),
    ("email-provider", "Email delivery service", "Sends marketing and transactional email."),
    ("sms-provider", "SMS gateway", "Sends text messages to customers and drivers."),
    ("address-lookup", "Address lookup", "Finds and checks postal addresses."),
    ("identity-provider", "Workforce identity provider", "Single sign-on for staff."),
    ("ad-platform", "Advertising platform", "Receives audiences and returns campaign results."),
    ("supplier-portal", "Supplier portal", "Where suppliers see orders and send invoices."),
    ("edi-network", "EDI network", "Carries purchase orders and advance shipping notices to suppliers."),
    ("telematics", "Telematics provider", "Vehicle positions and driver behaviour."),
    ("monitoring", "Monitoring service", "Logs, metrics and alerts."),
    ("cloud-storage", "Cloud object storage", "Holds files dropped between systems and the data lake."),
    ("weather", "Weather data feed", "Forecasts used in demand planning."),
    ("contact-centre", "Contact centre platform", "Telephony and chat for customer service."),
    ("review-platform", "Product review platform", "Collects and serves product reviews."),
]

entries = {}       # id -> dict
bodies = {}        # id -> markdown body


def put(id, data, body):
    entries[id] = data
    bodies[id] = body


def slug(s):
    return re.sub(r"[^a-z0-9]+", "-", s.lower()).strip("-")


def para(text):
    return textwrap.fill(text, 1000)


def commit():
    return "".join(random.choice("0123456789abcdef") for _ in range(7))


def pick_type(legacy):
    r, acc = random.random(), 0
    for t, w in (LEGACY if legacy else MODERN):
        acc += w
        if r <= acc:
            return t
    return "service"


# ---------- systems ----------
for key, name, project, team, *_ in DOMAINS:
    put(f"systems/{key}", {"kind": "system", "name": name, "summary": f"Everything {team} looks after for {name.lower()}.",
                            "sources": {"said": f"person:architecture@2026-09-30"}, "owner": {"name": team, "basis": "stated", "source": "said"}},
        f"## Overview\n\n{name} as the architecture team describes it: the services, jobs and databases {team} owns.")

# ---------- externals ----------
for key, name, summary in EXTERNALS:
    put(f"external/{key}", {"kind": "external", "name": name, "summary": summary, "sources": {"said": "person:architecture@2026-09-30"}},
        f"## Overview\n\n{summary} An outside company; Kestrel holds a contract with it.")

# ---------- databases and tables ----------
db_tables = {}     # db id -> [table ids]
db_routines = {}
db_domain = {}
for key, name, project, team, repos, dbs, legacy in DOMAINS:
    for db in dbs:
        dbid = f"databases/{slug(db)}/database"
        db_domain[dbid] = key
        nouns = NOUNS[key]
        n_tables = random.randint(25, 160) if key not in ("data",) else random.randint(150, 400)
        tables = []
        seen = set()
        for i in range(n_tables):
            base = random.choice(nouns).replace("-", "_")
            suffix = random.choice(["", "_line", "_history", "_status", "_audit", "_link", "_type", "_staging", "_archive", "_detail", "_summary"])
            tname = f"{base}{suffix}"
            while tname in seen:
                tname = f"{base}{suffix}_{len(seen)}"
            seen.add(tname)
            schema = "dbo" if random.random() < 0.8 else random.choice(["stg", "rpt", "app"])
            tid = f"databases/{slug(db)}/tables/{schema}/{slug(tname)}"
            src = f"{slug(project)}-{slug(db)}-db:schema/{schema}/{tname}.sql@{commit()}"
            t = {"kind": "table", "name": f"{schema}.{tname}", "summary": f"{tname.replace('_', ' ').capitalize()} records for {name.lower()}.",
                 "database": dbid, "sources": {"scripts": src}}
            if tables and random.random() < 0.5:
                t["links"] = [{"type": "references", "to": random.choice(tables), "name": f"FK_{tname}_{i}", "basis": "extracted", "source": "scripts#L12"}]
            if random.random() < 0.12:
                t["tags"] = [{"name": random.choice(["customer-data", "payment-data", "employee-data", "order-data", "stock-data", "supplier-data"]), "basis": "stated", "source": "person:dpo@2026-10-14"}]
            put(tid, t, f"## Overview\n\nOne row per {base.replace('_', ' ')}{' ' + suffix.strip('_') if suffix else ''} in {db}.")
            tables.append(tid)
        db_tables[dbid] = tables
        routines = []
        for i in range(random.randint(5, 60) if legacy > 0.2 else random.randint(0, 12)):
            rname = f"usp_{random.choice(nouns).replace('-', '_')}_{random.choice(['get', 'save', 'close', 'recalc', 'purge', 'load', 'post'])}_{i}"
            rid = f"databases/{slug(db)}/routines/dbo/{slug(rname)}"
            links = []
            for tt in random.sample(tables, min(len(tables), random.randint(1, 4))):
                links.append({"type": random.choice(["reads", "writes"]), "to": tt, "basis": "extracted", "source": f"code#L{random.randint(5, 300)}"})
            put(rid, {"kind": "routine", "type": "procedure", "name": f"dbo.{rname}", "summary": f"Stored procedure in {db}.", "database": dbid,
                      "sources": {"code": f"{slug(project)}-{slug(db)}-db:procs/{rname}.sql@{commit()}"}, "links": links},
                f"## Overview\n\nA stored procedure in {db}.")
            routines.append(rid)
        db_routines[dbid] = routines
        put(dbid, {"kind": "database", "name": db, "summary": f"The {db} database for {name.lower()}.", "engine": "sqlserver",
                   "systems": [{"id": f"systems/{key}", "basis": "stated", "source": "person:architecture@2026-09-30"}],
                   "owner": {"name": team, "basis": "stated", "source": "person:architecture@2026-09-30"}},
            f"## Overview\n\n{db} holds {n_tables} tables for {name.lower()}.")

# ---------- channels ----------
channels = {}      # domain -> [channel ids]
for key, name, project, team, *_ in DOMAINS:
    chs = []
    for noun in random.sample(NOUNS[key], min(len(NOUNS[key]), random.randint(4, 9))):
        kind = random.choices(["topic", "queue", "file-drop"], [0.6, 0.25, 0.15])[0]
        cname = f"{key}.{noun}-{'events' if kind == 'topic' else 'requests' if kind == 'queue' else 'files'}"
        cid = f"channels/{slug(cname)}"
        put(cid, {"kind": "channel", "type": kind, "name": cname, "summary": f"{noun.replace('-', ' ').capitalize()} {'events from' if kind == 'topic' else 'work for' if kind == 'queue' else 'files dropped by'} {name.lower()}.",
                  "sources": {"said": "person:integration@2026-10-01"}},
            f"## Overview\n\nA {kind.replace('-', ' ')} carrying {noun.replace('-', ' ')} data.")
        chs.append(cid)
    channels[key] = chs

# ---------- repositories and components ----------
components = {}    # domain -> [component ids]
libraries = []     # platform and domain libraries
repo_count = 0
for key, name, project, team, n_repos, dbs, legacy in DOMAINS:
    comps = []
    for r in range(n_repos * SCALE):
        is_legacy = random.random() < legacy
        noun = random.choice(NOUNS[key])
        folder = slug(f"{project}-{noun}-{random.choice(['service', 'app', 'jobs', 'portal', 'core', 'sync', 'api', 'batch', 'tools', 'web'])}")
        while f"repositories/{folder}/repository" in entries:
            folder = f"{folder}-{r}"
        on_github = not is_legacy and random.random() < 0.55
        location = f"github:{ORG}/{folder}" if on_github else f"azdo:{ORG}/{project}/{folder}"
        stack = STACKS[random.choice(["vb6", "asp", "older"])] if is_legacy else STACKS["modern"]
        c = commit()
        put(f"repositories/{folder}/repository", {"kind": "repository", "name": folder, "summary": f"{noun.replace('-', ' ').capitalize()} code for {name.lower()}.",
                                                   "stack": stack, "locations": [{"id": location, "primary": True}],
                                                   "verified": {"commit": c, "date": TODAY, "cartographer": "0.1.0"}},
            f"## Overview\n\nOwned by the {team}.")
        repo_count += 1
        for k in range(random.choices([1, 2, 3, 4], [0.45, 0.3, 0.17, 0.08])[0]):
            ctype = pick_type(is_legacy)
            if key == "platform":
                ctype = "library" if random.random() < 0.6 else "service"
            cname = f"{noun}-{ctype}" if k == 0 else f"{noun}-{ctype}-{k + 1}"
            cid = f"repositories/{folder}/components/{slug(cname)}"
            if cid in entries:
                continue
            comp = {"kind": "component", "type": ctype, "name": f"Kestrel.{name.split()[0]}.{''.join(p.capitalize() for p in cname.split('-'))}" if not is_legacy else cname.upper().replace("-", "_"),
                    "summary": f"{ctype.replace('-', ' ').capitalize()} for {noun.replace('-', ' ')} in {name.lower()}.", "repository": f"repositories/{folder}/repository",
                    "stack": stack, "sources": {"code": f"{folder}:src/{cname}/Program.cs@{c}"},
                    "systems": [{"id": f"systems/{key}", "basis": "stated", "source": "person:architecture@2026-09-30"}], "links": [],
                    "verified": {"commit": c, "date": TODAY, "cartographer": "0.1.0"}}
            put(cid, comp, f"## Overview\n\nA {ctype.replace('-', ' ')} owned by the {team}.")
            comps.append(cid)
            if ctype == "library":
                libraries.append(cid)
    components[key] = comps

DOMAIN_KEYS = [d[0] for d in DOMAINS]
DBS_OF = {d[0]: [f"databases/{slug(x)}/database" for x in d[5]] for d in DOMAINS}
NEIGHBOURS = {  # who trades with whom, beyond everyone feeding data and finance
    "customer": ["ecom", "loyalty", "marketing", "service", "credit", "identity"],
    "ecom": ["customer", "orders", "pricing", "catalog", "inventory", "loyalty", "credit"],
    "stores": ["pricing", "catalog", "inventory", "orders", "payroll", "loyalty"],
    "orders": ["ecom", "warehouse", "transport", "finance", "service", "inventory"],
    "pricing": ["catalog", "stores", "ecom", "vendors"],
    "catalog": ["vendors", "pricing", "ecom", "stores"],
    "inventory": ["warehouse", "stores", "vendors", "orders", "transport"],
    "warehouse": ["inventory", "transport", "orders", "payroll"],
    "transport": ["warehouse", "orders", "stores"],
    "vendors": ["finance", "catalog", "inventory"],
    "finance": ["vendors", "payroll", "orders", "credit", "stores"],
    "payroll": ["finance", "stores", "warehouse"],
    "loyalty": ["customer", "marketing", "stores", "ecom"],
    "credit": ["customer", "finance", "service"],
    "service": ["customer", "orders", "credit"],
    "marketing": ["customer", "loyalty", "ecom"],
    "data": DOMAIN_KEYS,
    "identity": ["customer", "ecom", "platform"],
    "platform": [],
}
EXT_OF = {
    "ecom": ["card-acquirer", "tax-engine", "fraud-screening", "address-lookup", "review-platform"], "stores": ["card-acquirer", "tax-engine"],
    "orders": ["parcel-carrier-a", "parcel-carrier-b", "tax-engine"], "transport": ["parcel-carrier-a", "pallet-network", "telematics", "sms-provider"],
    "vendors": ["edi-network", "supplier-portal"], "finance": ["bank-payments", "tax-authority", "open-banking"], "payroll": ["payroll-bureau", "tax-authority", "bank-payments"],
    "credit": ["credit-bureau", "open-banking", "fraud-screening", "card-acquirer"], "service": ["contact-centre", "sms-provider"],
    "marketing": ["email-provider", "sms-provider", "ad-platform"], "inventory": ["weather"], "customer": ["address-lookup", "email-provider"],
    "identity": ["identity-provider"], "data": ["cloud-storage"], "platform": ["monitoring"],
}


def link(cid, l):
    links = entries[cid]["links"]
    ident = (l["type"], l.get("to"))
    if ident not in {(x["type"], x.get("to")) for x in links}:
        links.append(l)


def over_channel(ch, legacy):
    kind = entries[ch]["type"]
    return "kafka" if kind == "topic" else ("msmq" if legacy else "amqp") if kind == "queue" else random.choice(["sftp", "smb"])


def api_link(to, legacy, b):
    """A call to another service: what it does decides the direction, the stack decides the transport."""
    kind = random.choices(["reads", "writes", "calls"], [0.5, 0.35, 0.15])[0]
    how = random.choice(["soap", "com", "remoting"]) if legacy else random.choices(["http", "grpc", "soap"], [0.75, 0.15, 0.1])[0]
    noun = entries[to]["name"].split(".")[-1].lower()
    op = {"reads": f"GET /{noun}", "writes": random.choice([f"POST /{noun}", f"PUT /{noun}/{{id}}"]), "calls": f"{noun}.Invoke"}[kind]
    return {"type": kind, "to": to, "how": how, "uses": [op], "basis": b, "source": src()}


def src():
    return f"code#L{random.randint(10, 400)}"


for key in DOMAIN_KEYS:
    for cid in components[key]:
        ctype = entries[cid]["type"]
        legacy = entries[cid]["stack"][0] in ("vb6", "classic-asp")
        basis = lambda: random.choices(["extracted", "inferred"], [0.8, 0.2])[0]
        # code dependencies: platform libraries everywhere, a domain library sometimes
        if not legacy:
            pool = [l for l in libraries if l.startswith("repositories/platform-")] or libraries
            for lib in random.sample(pool, k=min(3, len(pool))):
                if lib != cid:
                    link(cid, {"type": "depends-on", "to": lib, "name": entries[lib]["name"], "basis": "extracted", "source": src()})
        if ctype == "library":
            continue
        # own databases: reads and writes
        for db in DBS_OF[key]:
            for t in random.sample(db_tables[db], min(len(db_tables[db]), random.randint(1, 6 if not legacy else 12))):
                link(cid, {"type": random.choices(["reads", "writes"], [0.6, 0.4])[0], "to": t, "how": "sql", "basis": basis(), "source": src()})
            if db_routines[db] and random.random() < (0.7 if legacy else 0.2):
                link(cid, {"type": "calls", "to": random.choice(db_routines[db]), "how": "sql", "basis": "extracted", "source": src()})
        # events: services publish their domain's channels, and consume neighbours'
        if ctype in ("service", "job", "etl", "site") and channels[key] and random.random() < 0.7:
            ch = random.choice(channels[key])
            link(cid, {"type": "publishes-to", "to": ch, "how": over_channel(ch, legacy), "basis": basis(), "source": src()})
        for other in random.sample(NEIGHBOURS[key], min(len(NEIGHBOURS[key]), random.randint(0, 2))):
            if channels.get(other) and random.random() < 0.6:
                ch = random.choice(channels[other])
                link(cid, {"type": "consumes-from", "to": ch, "how": over_channel(ch, legacy), "basis": basis(), "source": src()})
        # calls inside the domain, and now and then to a neighbour's service
        peers = [c for c in components[key] if c != cid and entries[c]["type"] == "service"]
        if peers and random.random() < 0.5:
            link(cid, api_link(random.choice(peers), legacy, basis()))
        if NEIGHBOURS[key] and random.random() < 0.25:
            other = random.choice(NEIGHBOURS[key])
            svc = [c for c in components.get(other, []) if entries[c]["type"] == "service"]
            if svc:
                link(cid, api_link(random.choice(svc), legacy, basis()))
        # legacy reaches straight into other domains' tables
        if legacy and NEIGHBOURS[key] and random.random() < 0.5:
            other = random.choice(NEIGHBOURS[key])
            for db in DBS_OF.get(other, [])[:1]:
                link(cid, {"type": "reads", "to": random.choice(db_tables[db]), "how": "sql", "basis": "extracted", "source": src()})
        # outside companies
        for ext in EXT_OF.get(key, []):
            if random.random() < 0.15:
                link(cid, {"type": random.choice(["sends-to", "receives-from"]), "to": f"external/{ext}", "how": random.choices(["http", "sftp", "soap"], [0.7, 0.2, 0.1])[0], "basis": basis(), "source": src()})

# the data platform reads every database and loads its own
loaders = [c for c in components["data"] if entries[c]["type"] in ("etl", "job")]
for db, tables in db_tables.items():
    if db_domain[db] == "data":
        continue
    for t in random.sample(tables, min(len(tables), 8)):
        link(random.choice(loaders), {"type": "reads", "to": t, "how": "sql", "basis": "extracted", "source": src()})
for c in loaders:
    for db in DBS_OF["data"]:
        for t in random.sample(db_tables[db], 5):
            link(c, {"type": "writes", "to": t, "how": "sql", "basis": "extracted", "source": src()})

# gateways: a pass-through in front of an old and a new service, so callers never change address while work moves
for key in ["ecom", "orders", "credit", "pricing"]:
    svcs = [c for c in components[key] if entries[c]["type"] == "service"]
    if len(svcs) < 2:
        continue
    old, new = random.sample(svcs, 2)
    project = next(d[2] for d in DOMAINS if d[0] == key)
    folder, c = slug(f"{project}-gateway"), commit()
    gid = f"repositories/{folder}/components/{folder}"
    put(f"repositories/{folder}/repository", {"kind": "repository", "name": folder, "summary": f"The gateway in front of {entries[old]['name']} and {entries[new]['name']}.",
                                               "stack": STACKS["modern"], "locations": [{"id": f"github:{ORG}/{folder}", "primary": True}],
                                               "verified": {"commit": c, "date": TODAY, "cartographer": "0.1.0"}}, "## Overview\n\nA reverse proxy.")
    put(gid, {"kind": "component", "type": "service", "name": f"Kestrel.{key.capitalize()}.Gateway", "summary": f"Passes requests to {entries[old]['name']} and {entries[new]['name']} unchanged.",
              "repository": f"repositories/{folder}/repository", "stack": STACKS["modern"], "sources": {"routes": f"{folder}:src/appsettings.json@{c}"},
              "systems": [{"id": f"systems/{key}", "basis": "stated", "source": "person:architecture@2026-09-30"}],
              "links": [{"type": "forwards-to", "to": t, "how": h, "uses": [f"/{key}/*"], "basis": "extracted", "source": "routes#L12"} for t, h in [(old, "soap"), (new, "http")]],
              "verified": {"commit": c, "date": TODAY, "cartographer": "0.1.0"}}, "## Overview\n\nA YARP reverse proxy.")
    components[key].append(gid)
    # callers from outside the domain now go through the gateway
    for cs in components.values():
        for cid in cs:
            if cid.split("/")[1].startswith(slug(project)) or cid == gid:
                continue
            for l in entries[cid]["links"]:
                if l.get("to") in (old, new) and l["type"] in ("reads", "writes") and (l["type"], gid) not in {(x["type"], x.get("to")) for x in entries[cid]["links"]}:
                    l["to"] = gid

# lookups: services many parts read from and nothing feeds but their own database
for key in ["pricing", "identity", "catalog", "customer"]:
    svcs = [c for c in components[key] if entries[c]["type"] == "service" and not c.endswith("-gateway")]
    if not svcs:
        continue
    lid = random.choice(svcs)
    entries[lid]["links"] = [l for l in entries[lid]["links"] if l["type"] not in ("consumes-from", "receives-from") and not (l["type"] in ("reads", "calls") and l.get("to", "").startswith("repositories/"))]
    # whatever pointed at it now reads it; a gateway in front of it keeps forwarding, and a part keeps one read
    for cs in components.values():
        for cid in cs:
            kept, read = [], False
            for l in entries[cid]["links"]:
                if l.get("to") == lid and l["type"] not in ("depends-on", "forwards-to"):
                    if read:
                        continue
                    l["type"], l["uses"], read = "reads", [l.get("uses", [f"GET /{key}"])[0].replace("POST", "GET").replace("PUT", "GET")], True
                kept.append(l)
            entries[cid]["links"] = kept
    readers = random.sample([c for cs in components.values() for c in cs if entries[c]["type"] != "library" and c != lid], 25)
    for r in readers:
        link(r, {"type": "reads", "to": lid, "how": "http", "uses": [f"GET /{key}/{{id}}"], "basis": random.choice(["extracted", "inferred"]), "source": src()})

# a few things nobody has worked out yet
for cid in random.sample([c for cs in components.values() for c in cs], 60):
    bodies[cid] += ("\n\n## Open questions\n\n### q1\n\n- State: needs-person\n- Raised by: agent\n- Attempts: 3\n"
                    "- Missing: Which team still runs this, and whether it is still used in production.\n- Waiting on: person\n"
                    "- Answer looks like: A team name and a yes or no.\n- Answer:")

# ---------- write ----------
ORDER = ["kind", "name", "type", "summary", "status", "repository", "database", "engine", "stack", "locations", "sources", "systems", "owner", "tags", "links", "verified"]


def yaml_value(v, indent):
    pad = " " * indent
    if isinstance(v, dict):
        return "\n" + "\n".join(f"{pad}{k}:{yaml_value(x, indent + 2)}" for k, x in v.items())
    if isinstance(v, list):
        out = []
        for item in v:
            if isinstance(item, dict):
                first, *rest = list(item.items())
                lines = [f"{pad}- {first[0]}:{yaml_value(first[1], indent + 4)}"] + [f"{pad}  {k}:{yaml_value(x, indent + 4)}" for k, x in rest]
                out.append("\n".join(lines))
            else:
                out.append(f"{pad}- {scalar(item)}")
        return "\n" + "\n".join(out)
    return " " + scalar(v)


def scalar(v):
    s = str(v)
    if s == "" or any(ch in s for ch in ":#{}[],&*!|>'\"%@`") or s[0] in "-?" or s.lower() in ("yes", "no", "true", "false", "null") or s.replace(".", "").isdigit():
        return '"' + s.replace('"', '\\"') + '"'
    return s


for id, data in entries.items():
    if data.get("links") == []:
        del data["links"]
    path = os.path.join(OUT, *id.split("/")) + ".md"
    os.makedirs(os.path.dirname(path), exist_ok=True)
    front = "\n".join(f"{k}:{yaml_value(data[k], 2)}" for k in ORDER if k in data)
    with open(path, "w") as f:
        f.write(f"---\n{front}\n---\n\n{bodies[id]}\n")

with open(os.path.join(OUT, "cartographer.yml"), "w") as f:
    f.write(f"organisation: Kestrel Group\ngithub: {{organisation: {ORG}}}\nazure-devops: {{organisation: {ORG}}}\ninternal-prefixes: [\"Kestrel.*\"]\n")

kinds = {}
for e in entries.values():
    kinds[e["kind"]] = kinds.get(e["kind"], 0) + 1
print(f"wrote {len(entries)} entries to {OUT}: " + ", ".join(f"{n} {k}" for k, n in sorted(kinds.items())))
