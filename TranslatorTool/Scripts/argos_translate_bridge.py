import json
import sys
import traceback

if hasattr(sys.stdout, "reconfigure"):
    sys.stdout.reconfigure(encoding="utf-8")
if hasattr(sys.stderr, "reconfigure"):
    sys.stderr.reconfigure(encoding="utf-8")


def write(payload):
    sys.stdout.write(json.dumps(payload, ensure_ascii=False))
    sys.stdout.flush()


def fail(message, detail=""):
    write({"ok": False, "translatedText": "", "error": message, "detail": detail})


def translate_payload(request):
    text = (request.get("text") or "").strip()
    from_code = request.get("from") or "en"
    to_code = request.get("to") or "zh"
    preserve_lines = bool(request.get("preserveLines"))
    if not text:
        return {"ok": True, "translatedText": "", "error": "", "detail": "empty input"}

    try:
        import argostranslate.settings
        argostranslate.settings.chunk_type = argostranslate.settings.ChunkType.MINISBD
        import argostranslate.translate
    except ModuleNotFoundError:
        return {"ok": False, "translatedText": "", "error": "Argos Translate is not installed.", "detail": "Run scripts/install-argos-en-zh.ps1 first."}

    installed_languages = argostranslate.translate.get_installed_languages()
    from_language = next((lang for lang in installed_languages if lang.code == from_code), None)
    to_language = next((lang for lang in installed_languages if lang.code == to_code), None)
    if from_language is None or to_language is None:
        return {"ok": False, "translatedText": "", "error": f"Argos language model {from_code}->{to_code} is not installed.", "detail": ""}

    translation = from_language.get_translation(to_language)
    if preserve_lines:
        lines = text.splitlines()
        translated_lines = [translation.translate(line).strip() if line.strip() else "" for line in lines]
        translated_text = "\n".join(translated_lines).strip()
    else:
        translated_text = translation.translate(text).strip()

    return {"ok": True, "translatedText": translated_text, "error": "", "detail": "Argos Translate"}


def main():
    try:
        request = json.loads(sys.stdin.read() or "{}")
        write(translate_payload(request))
    except Exception:
        fail("Argos Translate failed.", traceback.format_exc(limit=6))


if __name__ == "__main__":
    main()
