from pathlib import Path

from pydantic_settings import BaseSettings, SettingsConfigDict

ROOT_DIR = Path(__file__).resolve().parent.parent

# re-export for settings_store
__all__ = ["ROOT_DIR", "Settings", "settings"]


class Settings(BaseSettings):
    model_config = SettingsConfigDict(
        env_file=ROOT_DIR / ".env",
        env_file_encoding="utf-8",
        extra="ignore",
    )

    limis_base_url: str = "http://10.1.228.22"
    limis_username: str = ""
    limis_password: str = ""

    report_template: str = "环刀300.docx"
    record_template: str = "环刀（2个1组）.docx"

    @property
    def report_template_path(self) -> Path:
        return ROOT_DIR / self.report_template

    @property
    def record_template_path(self) -> Path:
        return ROOT_DIR / self.record_template


settings = Settings()
