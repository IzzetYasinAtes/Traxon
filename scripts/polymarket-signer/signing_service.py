from flask import Flask, request, jsonify
import os
import sys
import logging

app = Flask(__name__)
logging.basicConfig(level=logging.INFO, format='%(asctime)s [%(levelname)s] %(message)s')
logger = logging.getLogger(__name__)

# Lazy client initialization
_client = None


def get_client():
    global _client
    if _client is not None:
        return _client

    from py_clob_client.client import ClobClient

    private_key = os.environ.get("POLY_PRIVATE_KEY", "")
    funder = os.environ.get("POLY_FUNDER_ADDRESS", "")
    chain_id = int(os.environ.get("POLY_CHAIN_ID", "137"))
    sig_type = int(os.environ.get("POLY_SIGNATURE_TYPE", "0"))

    if not private_key:
        logger.error("POLY_PRIVATE_KEY not set")
        return None

    try:
        _client = ClobClient(
            host="https://clob.polymarket.com",
            key=private_key,
            chain_id=chain_id,
            signature_type=sig_type,
            funder=funder if funder else None
        )

        # L1 Auth - API credentials turet
        creds = _client.create_or_derive_api_creds()
        _client.set_api_creds(creds)

        logger.info("Polymarket client initialized successfully")
        return _client
    except Exception as e:
        logger.error(f"Failed to initialize client: {e}")
        _client = None
        return None


@app.route("/health", methods=["GET"])
def health():
    client = get_client()
    return jsonify({
        "status": "healthy" if client else "no_credentials",
        "service": "polymarket-signer"
    })


@app.route("/create-and-post", methods=["POST"])
def create_and_post():
    """Order olustur, EIP-712 ile imzala ve Polymarket'e gonder"""
    client = get_client()
    if not client:
        return jsonify({"success": False, "error": "Client not initialized - check credentials"}), 503

    try:
        data = request.json

        from py_clob_client.clob_types import OrderArgs, MarketOrderArgs, OrderType
        from py_clob_client.order_builder.constants import BUY, SELL

        order_type_str = data.get("order_type", "GTC")
        order_type = getattr(OrderType, order_type_str, OrderType.GTC)
        side = BUY if data.get("side") == "BUY" else SELL

        # FAK/FOK = market order (amount in dollars), GTC/GTD = limit order (size in shares)
        if order_type_str in ("FAK", "FOK"):
            required = ["token_id", "amount", "side"]
            for field in required:
                if field not in data:
                    return jsonify({"success": False, "error": f"Missing field: {field}"}), 400

            order_args = MarketOrderArgs(
                token_id=data["token_id"],
                amount=float(data["amount"]),
                side=side
            )

            response = client.create_and_post_market_order(
                order_args=order_args,
                order_type=order_type
            )
        else:
            required = ["token_id", "price", "size", "side"]
            for field in required:
                if field not in data:
                    return jsonify({"success": False, "error": f"Missing field: {field}"}), 400

            order_args = OrderArgs(
                token_id=data["token_id"],
                price=float(data["price"]),
                size=float(data["size"]),
                side=side
            )

            response = client.create_and_post_order(
                order_args=order_args,
                order_type=order_type
            )

        logger.info(f"Order posted: {response}")

        return jsonify({
            "success": True,
            "response": response
        })

    except Exception as e:
        logger.error(f"Order error: {e}")
        return jsonify({"success": False, "error": str(e)}), 400


@app.route("/cancel", methods=["POST"])
def cancel_order():
    """Order iptal et"""
    client = get_client()
    if not client:
        return jsonify({"success": False, "error": "Client not initialized"}), 503

    try:
        data = request.json
        order_id = data.get("order_id", "")

        if not order_id:
            return jsonify({"success": False, "error": "Missing order_id"}), 400

        response = client.cancel(order_id)

        return jsonify({"success": True, "response": response})

    except Exception as e:
        logger.error(f"Cancel error: {e}")
        return jsonify({"success": False, "error": str(e)}), 400


@app.route("/balance", methods=["GET"])
def get_balance():
    """USDC bakiye sorgula"""
    client = get_client()
    if not client:
        return jsonify({"success": False, "error": "Client not initialized"}), 503

    try:
        bal = client.get_balance_allowance()
        balance_usdc = float(bal.get("balance", 0)) / 1e6

        return jsonify({
            "success": True,
            "balance_usdc": balance_usdc
        })

    except Exception as e:
        logger.error(f"Balance error: {e}")
        return jsonify({"success": False, "error": str(e)}), 400


if __name__ == "__main__":
    port = int(os.environ.get("SIGNER_PORT", "5099"))
    logger.info(f"Starting Polymarket signing service on port {port}")
    app.run(host="127.0.0.1", port=port, debug=False)
