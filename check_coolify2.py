#!/usr/bin/env python3
"""Check and trigger Coolify deployments for casasim apps"""
import os, json, urllib.request, sys

url = os.environ.get("COOLIFY_URL", "https://settings.casadosmoreira.com")
token = os.environ.get("COOLIFY_TOKEN", "")
h = {"Authorization": f"Bearer {token}", "Content-Type": "application/json"}

apps = [
    ("casasim-pt:main", "v5ko657tt9jq05vhstgwq3ke"),
    ("casasim-pt", "b1ofmse0czbp0is4c9z80j0k"),
]

for name, uuid in apps:
    print(f"\n=== {name} ({uuid}) ===")
    
    # Check app status
    req = urllib.request.Request(f"{url}/api/v1/applications/{uuid}", headers=h, method="GET")
    try:
        resp = urllib.request.urlopen(req)
        data = json.loads(resp.read())
        print(f"  Name: {data.get('name', '?')}")
        print(f"  Status: {data.get('status', '?')}")
        print(f"  FQDN: {data.get('fqdn', '-')}")
        print(f"  Last deploy: {data.get('deploymentUuid', '-')}")
    except Exception as ex:
        print(f"  Status check failed: {ex}")
    
    # Check deployments
    req2 = urllib.request.Request(f"{url}/api/v1/applications/{uuid}/deployments", headers=h, method="GET")
    try:
        resp2 = urllib.request.urlopen(req2)
        deploys = json.loads(resp2.read())
        if isinstance(deploys, list):
            for d in deploys[:3]:
                duuid = d.get("uuid", "?")
                dstatus = d.get("status", "?")
                dcreated = d.get("createdAt", "?")
                print(f"  Deploy: {duuid} status={dstatus} created={dcreated[:19] if dcreated else '?'}")
        else:
            print(f"  Deployments: {json.dumps(deploys, indent=2)[:500]}")
    except Exception as ex:
        print(f"  Deployments check failed: {ex}")

# Trigger deployment on casasim-pt:main (the main preview app)
print("\n\nTriggering deployment on casasim-pt:main (v5ko657tt9jq05vhstgwq3ke)...")
deploy_req = urllib.request.Request(
    f"{url}/api/v1/deploy?uuid=v5ko657tt9jq05vhstgwq3ke",
    headers=h, method="POST"
)
try:
    resp = urllib.request.urlopen(deploy_req)
    result = json.loads(resp.read())
    print(f"Result: {json.dumps(result, indent=2)}")
except Exception as ex:
    print(f"Deploy trigger failed: {ex}")
