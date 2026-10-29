"""Run in a virtual environment with the installed yt-dlp on PYTHONPATH."""
import subprocess
import unittest
from types import SimpleNamespace
from unittest.mock import Mock, patch

from yt_dlp.extractor.youtube.pot._director import PoTokenRequestDirector
from yt_dlp_plugins.extractor.getpot_bgutil_script import (
    BgUtilScriptDenoPTP,
    BgUtilScriptNodePTP,
)


class BgutilTimeoutTests(unittest.TestCase):
    def provider(self, cls, runtime):
        provider = object.__new__(cls)
        provider.__dict__['_jsrt_path'] = runtime
        provider.__dict__['_script_path'] = '/test/' + provider._SCRIPT_BASENAME
        provider.__dict__['_server_home'] = '/test'
        provider.__dict__['_script_cache_dir'] = '/test/cache'
        provider.logger = Mock()
        provider._check_script = provider._check_script_impl
        return provider

    def test_version_timeout_selects_next_provider(self):
        deno = self.provider(BgUtilScriptDenoPTP, 'deno')
        node = self.provider(BgUtilScriptNodePTP, 'node')
        director = object.__new__(PoTokenRequestDirector)
        director.providers = {'deno': deno, 'node': node}
        director.preferences = [lambda p, r: p._JSRT_PREF]
        director.logger = SimpleNamespace(log_level=100, LogLevel=SimpleNamespace(TRACE=0))

        def run(command, **kwargs):
            if command[0] == 'deno':
                raise subprocess.TimeoutExpired(command, 15)
            return node.PROVIDER_VERSION, '', 0

        with patch('yt_dlp_plugins.extractor.getpot_bgutil_script.os.path.isfile', return_value=True), \
                patch('yt_dlp_plugins.extractor.getpot_bgutil_script.Popen.run', side_effect=run):
            self.assertEqual(list(director._get_providers(Mock())), [node])
        deno.logger.warning.assert_called_once()

    def test_other_errors_are_not_hidden(self):
        deno = self.provider(BgUtilScriptDenoPTP, 'deno')
        with patch('yt_dlp_plugins.extractor.getpot_bgutil_script.os.path.isfile', return_value=True), \
                patch('yt_dlp_plugins.extractor.getpot_bgutil_script.Popen.run', side_effect=PermissionError):
            with self.assertRaises(PermissionError):
                deno._check_script_impl(deno._script_path)


if __name__ == '__main__':
    unittest.main()
