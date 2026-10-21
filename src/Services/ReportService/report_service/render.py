"""Isolated PDF renderer. Never fetch network resources or client-selected files."""
from pathlib import Path
import sys

from weasyprint import HTML


def deny_resources(url, *args, **kwargs):
    raise ValueError('External resources are disabled in report templates')


def main():
    source, target = map(Path, sys.argv[1:3])
    HTML(filename=str(source), url_fetcher=deny_resources).write_pdf(str(target))


if __name__ == '__main__':
    main()
