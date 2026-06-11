#!/usr/bin/env python3
"""Check Coolify apps in CasaSim project"""
import os, json, urllib.request, sys

url = os.environ.get("COOLIFY_URL", "https://settings.casadosmoreira.com")
token = os.environ.get("COOLIFY_TOKEN", "")

headers = {
    "Authorization": f"Bearer {token}",
    "Content-Type": "application/json",
}

# Get project details including apps
req = urllib.request.Request(f"{url}/api/v1/projects/51", headers=headers)
resp = urllib.request.urlopen(req)
data = json.loads(resp.read())

for e in data.get("environments", []):
    ename = e.get("name", "?")
    print(f"Env: {ename}")
    for a in e.get("applications", []):
        aname = a.get("name", "?")
        auuid = a.get("uuid", "?")
        status = a.get("status", "?")
        deploy_uuid = a.get("deploymentUuid", "?")
        fqdn = a.get("fqdn", "")
        print(f"  App: {aname} (uuid: {auuid}, status: {status})")
        print(f"    FQDN: {fqdn}")
        print(f"    Last deploy: {deploy_uuid}")
