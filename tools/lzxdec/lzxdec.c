/* Decompress a raw LZX stream (as found in XEX2 "normal" compression) with libmspack.
 * Usage: lzxdec <in.lzx> <out.bin> <window_size> <out_size>
 */
#include <stdio.h>
#include <stdlib.h>
#include <string.h>

#include "system.h"
#include "lzx.h"

struct mem_file {
  unsigned char *buf;
  size_t size, pos;
};

static struct mspack_file *m_open(struct mspack_system *s, const char *f, int m) { return NULL; }
static void m_close(struct mspack_file *f) {}
static int m_read(struct mspack_file *f, void *buf, int n) {
  struct mem_file *m = (struct mem_file *)f;
  size_t left = m->size - m->pos;
  if ((size_t)n > left) n = (int)left;
  memcpy(buf, m->buf + m->pos, n);
  m->pos += n;
  return n;
}
static int m_write(struct mspack_file *f, void *buf, int n) {
  struct mem_file *m = (struct mem_file *)f;
  size_t left = m->size - m->pos;
  if ((size_t)n > left) n = (int)left;
  memcpy(m->buf + m->pos, buf, n);
  m->pos += n;
  return n;
}
static int m_seek(struct mspack_file *f, off_t o, int mode) { return -1; }
static off_t m_tell(struct mspack_file *f) { return (off_t)((struct mem_file *)f)->pos; }
static void m_msg(struct mspack_file *f, const char *fmt, ...) {}
static void *m_alloc(struct mspack_system *s, size_t n) { return malloc(n); }
static void m_free(void *p) { free(p); }
static void m_copy(void *src, void *dst, size_t n) { memcpy(dst, src, n); }

static struct mspack_system sys = {m_open, m_close, m_read, m_write, m_seek,
                                   m_tell, m_msg, m_alloc, m_free, m_copy, NULL};

int main(int argc, char **argv) {
  if (argc != 5) {
    fprintf(stderr, "usage: lzxdec <in> <out> <window_size> <out_size>\n");
    return 2;
  }
  unsigned long window = strtoul(argv[3], NULL, 0);
  size_t out_size = strtoul(argv[4], NULL, 0);
  int bits = 0;
  while ((1ul << bits) < window) bits++;

  FILE *fi = fopen(argv[1], "rb");
  if (!fi) return 3;
  fseek(fi, 0, SEEK_END);
  size_t in_size = ftell(fi);
  fseek(fi, 0, SEEK_SET);
  struct mem_file in = {malloc(in_size), in_size, 0};
  fread(in.buf, 1, in_size, fi);
  fclose(fi);
  struct mem_file out = {calloc(1, out_size), out_size, 0};

  struct lzxd_stream *lzx = lzxd_init(&sys, (struct mspack_file *)&in, (struct mspack_file *)&out,
                                      bits, 0, 0x8000, (off_t)out_size, 0);
  if (!lzx) return 4;
  int err = lzxd_decompress(lzx, (off_t)out_size);
  lzxd_free(lzx);
  if (err != MSPACK_ERR_OK) {
    fprintf(stderr, "lzx error %d after %zu bytes\n", err, out.pos);
    return 5;
  }
  FILE *fo = fopen(argv[2], "wb");
  fwrite(out.buf, 1, out_size, fo);
  fclose(fo);
  return 0;
}
