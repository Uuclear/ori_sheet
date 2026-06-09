from decimal import Decimal

from ring_knife.calc.ring_knife import _round2, calc_moisture


def test_round_half_even_to_even():
    assert _round2(Decimal("1.235")) == 1.24
    assert _round2(Decimal("1.225")) == 1.22


def test_moisture_bankers_rounding():
  # (30.4 - 26.9) / 26.9 * 100 = 13.011...
    rate = calc_moisture(15.2, 45.6, 42.1)
    assert rate == 13.01
