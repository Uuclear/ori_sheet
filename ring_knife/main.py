from contextlib import asynccontextmanager
from pathlib import Path

from fastapi import FastAPI
from fastapi.responses import FileResponse
from fastapi.staticfiles import StaticFiles

from ring_knife.api.limis_client import limis_client
from ring_knife.api.routes import router
from ring_knife.data_store import init_db
from ring_knife.settings_store import load_settings

STATIC_DIR = Path(__file__).parent / "templates" / "static"
TEMPLATES_DIR = Path(__file__).parent / "templates"


@asynccontextmanager
async def lifespan(_app: FastAPI):
    init_db()
    stored = load_settings()
    limis_client.configure(stored["base_url"], stored["username"], stored["password"])
    yield
    await limis_client.close()


app = FastAPI(title="环刀法压实度检测", version="1.1.0", lifespan=lifespan)
app.include_router(router)

if STATIC_DIR.exists():
    app.mount("/static", StaticFiles(directory=str(STATIC_DIR)), name="static")


@app.get("/")
async def tasks_page() -> FileResponse:
    return FileResponse(TEMPLATES_DIR / "tasks.html")


@app.get("/record")
async def record_page() -> FileResponse:
    return FileResponse(TEMPLATES_DIR / "index.html")


@app.get("/settings")
async def settings_page() -> FileResponse:
    return FileResponse(TEMPLATES_DIR / "settings.html")
