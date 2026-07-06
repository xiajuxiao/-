import json
import sys
import traceback

from argos_translate_bridge import translate_payload

if hasattr(sys.stdout, "reconfigure"):
    sys.stdout.reconfigure(encoding="utf-8")
if hasattr(sys.stderr, "reconfigure"):
    sys.stderr.reconfigure(encoding="utf-8")


def write(payload):
    sys.stdout.write(json.dumps(payload, ensure_ascii=False) + "\n")
    sys.stdout.flush()


write({"ready": True})

for line in sys.stdin:
    try:
        request = json.loads(line)
        if request.get("command") == "shutdown":
            write({"ok": True, "translatedText": "", "error": "", "detail": "shutdown"})
            break

        write(translate_payload(request))
    except Exception:
        write({"ok": False, "translatedText": "", "error": "Argos Translate failed.", "detail": traceback.format_exc(limit=6)})
