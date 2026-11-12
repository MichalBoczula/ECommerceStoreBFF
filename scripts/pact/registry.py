"""HTTPS client for the Pact registry's source CI publishers."""

import json
import os
from urllib.error import HTTPError
from urllib.parse import urlsplit
from urllib.request import HTTPRedirectHandler, Request, build_opener


class NoRedirect(HTTPRedirectHandler):
    def redirect_request(self, req, fp, code, msg, headers, newurl):
        return None


class Registry:
    def __init__(self):
        url = urlsplit(os.environ.get("PACT_REGISTRY_URL", ""))
        self.token = os.environ.get("PACT_PUBLISHER_TOKEN", "")
        if (
            url.scheme != "https" or not url.hostname or url.username is not None
            or url.password is not None or url.path not in ("", "/")
            or url.query or url.fragment or not self.token
        ):
            raise ValueError("Configure an HTTPS PACT_REGISTRY_URL origin and the BFF PACT_PUBLISHER_TOKEN.")
        self.origin = f"https://{url.netloc}"
        self.opener = build_opener(NoRedirect())

    def call(self, path, body=None, raw=False):
        headers = {} if body is None else {
            "Authorization": f"Bearer {self.token}", "Content-Type": "application/json",
        }
        req = Request(self.origin + path, data=None if body is None else json.dumps(body).encode(), headers=headers)
        try:
            with self.opener.open(req, timeout=90) as response:
                return response.read() if raw else json.load(response)
        except HTTPError as error:
            raise RuntimeError(f"Pact registry rejected {path.partition('?')[0]} with HTTP {error.code}.") from None
