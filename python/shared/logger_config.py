import logging
import sys

import structlog


class WhitelistFilter(logging.Filter):
    def __init__(self, allowed_name):
        super().__init__()
        self.allowed_name = allowed_name

    def filter(self, record):
        return record.name == self.allowed_name


def setup_logger(service_name):
    root_logger = logging.getLogger()

    if root_logger.hasHandlers():
        root_logger.handlers.clear()

    handler = logging.StreamHandler(sys.stdout)

    handler.setFormatter(logging.Formatter("%(message)s"))

    handler.addFilter(WhitelistFilter(service_name))

    root_logger.addHandler(handler)
    root_logger.setLevel(logging.DEBUG)

    structlog.configure(processors=[structlog.contextvars.merge_contextvars, structlog.stdlib.add_log_level,
                                    structlog.stdlib.add_logger_name, structlog.processors.TimeStamper(fmt="iso"),
                                    structlog.processors.dict_tracebacks, structlog.processors.JSONRenderer()],
                        wrapper_class=structlog.stdlib.BoundLogger, logger_factory=structlog.stdlib.LoggerFactory(),
                        cache_logger_on_first_use=True, )

    return structlog.get_logger(service_name)
