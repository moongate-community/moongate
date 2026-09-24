"""Run a TLS administration workflow using only generated protobuf contracts."""
import os
import secrets
from urllib.parse import urlparse

import grpc
from google.protobuf.empty_pb2 import Empty
from moongate.admin.v1 import accounts_pb2, accounts_pb2_grpc, auth_pb2, auth_pb2_grpc, server_pb2_grpc


def channel(endpoint):
    parsed = urlparse(endpoint)
    if parsed.scheme != "https" or not parsed.hostname or not parsed.port:
        raise ValueError("MOONGATE_ADMIN_ENDPOINT must be an https://host:port address")
    ca_path = os.environ.get("MOONGATE_ADMIN_CA")
    roots = open(ca_path, "rb").read() if ca_path else None
    return grpc.secure_channel(parsed.netloc, grpc.ssl_channel_credentials(root_certificates=roots))


def main():
    with channel(os.environ["MOONGATE_ADMIN_ENDPOINT"]) as login_channel:
        login = auth_pb2_grpc.AdminLoginStub(login_channel).Login(auth_pb2.LoginRequest(
            username=os.environ["MOONGATE_ADMIN_USERNAME"], password=os.environ["MOONGATE_ADMIN_PASSWORD"]), timeout=10)
        metadata = (("authorization", "Bearer " + login.access_token),)
        accounts = accounts_pb2_grpc.AdminAccountsStub(login_channel)
        created_name = "admin-sample-" + secrets.token_hex(6)
        created = accounts.CreateAccount(accounts_pb2.CreateAccountRequest(
            username=created_name, password=secrets.token_urlsafe(24)), metadata=metadata, timeout=10)
        # Reconcile through bounded keyset pages. No automatic mutation retries.
        cursor = 0
        found = False
        while True:
            page = accounts.ListAccounts(accounts_pb2.ListAccountsRequest(page_size=200, after_account_id=cursor), metadata=metadata, timeout=10)
            found |= any(account.account_id == created.account_id for account in page.accounts)
            cursor = page.next_after_account_id
            if cursor == 0:
                break
        if not found:
            raise RuntimeError("Created account missing from listing")
        game_endpoint = os.environ.get("MOONGATE_ADMIN_GAME_ENDPOINT", os.environ["MOONGATE_ADMIN_ENDPOINT"])
        with channel(game_endpoint) as game_channel:
            info = server_pb2_grpc.AdminServerStub(game_channel)
            info.GetServerInfo(Empty(), metadata=metadata, timeout=10)
            auth_pb2_grpc.AdminSessionStub(game_channel).Logout(Empty(), metadata=metadata, timeout=10)
            try:
                info.GetServerInfo(Empty(), metadata=metadata, timeout=10)
            except grpc.RpcError as error:
                if error.code() != grpc.StatusCode.UNAUTHENTICATED:
                    raise
            else:
                raise RuntimeError("Logged-out session was accepted")
    print("Administration workflow passed")


if __name__ == "__main__":
    main()
